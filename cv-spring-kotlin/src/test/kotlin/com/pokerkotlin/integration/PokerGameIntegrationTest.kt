package com.pokerkotlin.integration

import com.pokerkotlin.core.engine.GameEngine
import com.pokerkotlin.core.engine.GameEvent
import com.pokerkotlin.core.engine.GameState
import com.pokerkotlin.core.engine.PlayerAction
import com.pokerkotlin.core.engine.PlayerView
import com.pokerkotlin.core.engine.TableView
import com.pokerkotlin.core.model.Card
import com.pokerkotlin.core.model.Deck
import com.pokerkotlin.core.model.Hand
import com.pokerkotlin.core.model.Player
import com.pokerkotlin.core.model.PlayerId
import com.pokerkotlin.core.model.Rank
import com.pokerkotlin.core.model.Suit
import com.pokerkotlin.core.model.TableId
import com.pokerkotlin.core.strategy.BotStrategy
import com.pokerkotlin.server.repository.TableRepository
import com.pokerkotlin.server.session.SessionRegistry
import kotlinx.coroutines.CoroutineStart
import kotlinx.coroutines.launch
import kotlinx.coroutines.runBlocking
import kotlinx.coroutines.yield
import java.util.concurrent.atomic.AtomicInteger
import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertNotNull
import org.junit.jupiter.api.Assertions.assertNull
import org.junit.jupiter.api.Assertions.assertTrue
import org.junit.jupiter.api.Test

/**
 * Integration tests: full Texas Hold'em hand with 1 human player and 2 bots.
 *
 * ### Table layout
 * ```
 * seats:  [human(0), bot1(1), bot2(2)]
 * dealer: index 0  → SB = bot1, BB = bot2
 * UTG (first to act pre-flop) = human
 * ```
 *
 * ### Deterministic deck layout (11 cards)
 * ```
 * deck[0,1]   → human hole cards  : A♥  K♥
 * deck[2,3]   → bot1  hole cards  : 2♣  3♣
 * deck[4,5]   → bot2  hole cards  : 4♦  5♦
 * deck[6,7,8] → flop              : Q♥  J♥  T♥
 * deck[9]     → turn              : 9♣
 * deck[10]    → river             : 8♣
 * ```
 *
 * ### Best hands at showdown
 * | Player | Hole     | Best 5-card hand          | Category      |
 * |--------|----------|---------------------------|---------------|
 * | Human  | A♥ K♥   | A♥ K♥ Q♥ J♥ T♥           | ROYAL_FLUSH   |
 * | Bot1   | 2♣ 3♣   | Q♥ J♥ T♥ 9♣ 8♣           | STRAIGHT      |
 * | Bot2   | 4♦ 5♦   | Q♥ J♥ T♥ 9♣ 8♣           | STRAIGHT      |
 *
 * Human wins the pot.
 *
 * ### Pot at showdown
 * - Pre-flop: human Call 10 + bot1 Call 10 (SB 5 + call 5) + bot2 BB 10 = 30
 * - No further bets (bots always check) → final pot = 30
 * - Human: 1000 - 10 + 30 = 1020
 * - Bot1 : 1000 - 10 = 990
 * - Bot2 : 1000 - 10 = 990
 * - Total: 3000  (chips conserved ✓)
 */
class PokerGameIntegrationTest {

    // ── Test infrastructure ───────────────────────────────────────────────────

    /**
     * A bot that never folds: calls when chips are owed, otherwise checks.
     * Keeps the hand running all the way to Showdown regardless of hand strength.
     */
    private class AlwaysCallStrategy : BotStrategy {
        override fun decide(view: TableView, player: PlayerView): PlayerAction =
            if (view.toCall == 0) PlayerAction.Check(player.id)
            else PlayerAction.Call(player.id)
    }

    private val humanId = PlayerId("human")
    private val bot1Id  = PlayerId("bot1")
    private val bot2Id  = PlayerId("bot2")

    private fun human() = Player(id = humanId, name = "Alice", coins = 1_000, holeCards = Hand(emptyList()), isBot = false)
    private fun bot(id: PlayerId, name: String) = Player(id = id, name = name, coins = 1_000, holeCards = Hand(emptyList()), isBot = true)

    private fun card(rank: Rank, suit: Suit) = Card(rank, suit)

    /**
     * Deterministic deck: human gets a Royal Flush, bots get a Straight.
     *
     * Deck order mirrors the deal order inside [GameEngine.startHand]:
     * each player draws 2 cards sequentially, then flop/turn/river.
     */
    private fun royalFlushDeck() = Deck.of(
        // human hole cards
        card(Rank.ACE,   Suit.RED_HEART),   card(Rank.KING,  Suit.RED_HEART),
        // bot1 hole cards
        card(Rank.TWO,   Suit.GREEN_CLUB),  card(Rank.THREE, Suit.GREEN_CLUB),
        // bot2 hole cards
        card(Rank.FOUR,  Suit.BLUE_DIAMOND),card(Rank.FIVE,  Suit.BLUE_DIAMOND),
        // flop
        card(Rank.QUEEN, Suit.RED_HEART),   card(Rank.JACK,  Suit.RED_HEART),   card(Rank.TEN, Suit.RED_HEART),
        // turn
        card(Rank.NINE,  Suit.GREEN_CLUB),
        // river
        card(Rank.EIGHT, Suit.GREEN_CLUB),
    )

    /** Builds a [GameEngine] with 1 human + 2 AlwaysCall bots and the deterministic deck. */
    private fun buildEngine(): GameEngine {
        val engine = GameEngine(
            tableId     = TableId("integration-test"),
            blindAmount = 10,
            deckFactory = ::royalFlushDeck,
        )
        engine.addPlayer(human(), strategy = null)
        engine.addPlayer(bot(bot1Id, "Bot1"), strategy = AlwaysCallStrategy())
        engine.addPlayer(bot(bot2Id, "Bot2"), strategy = AlwaysCallStrategy())
        return engine
    }

    /**
     * Drives the human player through all four betting rounds.
     *
     * Pre-flop : human calls the BB (10 chips) — bots auto-play after.
     * Flop / Turn / River : human checks — bots auto-play first, human checks last.
     *
     * Returns the engine after the Showdown state is reached.
     */
    private suspend fun GameEngine.playFullHand(): GameEngine {
        startHand()
        // Pre-flop: human is UTG, must call the BB
        applyAction(PlayerAction.Call(humanId))
        // Flop, Turn, River: human acts last (post-flop queue: bot1 → bot2 → human)
        repeat(3) { applyAction(PlayerAction.Check(humanId)) }
        return this
    }

    // ── Phase progression ─────────────────────────────────────────────────────

    @Test
    fun `after startHand state is PreFlop and human is UTG`() = runBlocking {
        val engine = buildEngine()
        engine.startHand()

        val snap = engine.snapshot()
        assertTrue(snap.state is GameState.PreFlop, "Expected PreFlop, got ${snap.state}")
        assertEquals(humanId, snap.currentTurnId, "Human (UTG) should be first to act pre-flop")
    }

    @Test
    fun `human call advances game to Flop with 3 board cards`() = runBlocking {
        val engine = buildEngine()
        engine.startHand()
        engine.applyAction(PlayerAction.Call(humanId))   // bots auto-play → Flop

        val snap = engine.snapshot()
        assertTrue(snap.state is GameState.Flop, "Expected Flop after human calls, got ${snap.state}")
        assertEquals(3, snap.board.size, "Flop must have exactly 3 board cards")
        assertEquals(humanId, snap.currentTurnId, "Human should act last on post-flop streets")
    }

    @Test
    fun `human check on Flop advances to Turn with 4 board cards`() = runBlocking {
        val engine = buildEngine()
        engine.startHand()
        engine.applyAction(PlayerAction.Call(humanId))
        engine.applyAction(PlayerAction.Check(humanId))  // bots auto-play → Turn

        val snap = engine.snapshot()
        assertTrue(snap.state is GameState.Turn, "Expected Turn, got ${snap.state}")
        assertEquals(4, snap.board.size)
    }

    @Test
    fun `human check on Turn advances to River with 5 board cards`() = runBlocking {
        val engine = buildEngine()
        engine.startHand()
        engine.applyAction(PlayerAction.Call(humanId))
        engine.applyAction(PlayerAction.Check(humanId))
        engine.applyAction(PlayerAction.Check(humanId))  // bots auto-play → River

        val snap = engine.snapshot()
        assertTrue(snap.state is GameState.River, "Expected River, got ${snap.state}")
        assertEquals(5, snap.board.size)
    }

    @Test
    fun `full hand reaches Showdown after River check`() = runBlocking {
        val engine = buildEngine().playFullHand()
        assertTrue(engine.snapshot().state is GameState.Showdown, "Expected Showdown at end of hand")
    }

    // ── Card visibility ───────────────────────────────────────────────────────

    @Test
    fun `opponent hole cards are hidden from human before Showdown`() = runBlocking {
        val engine = buildEngine()
        engine.startHand()

        val snap = engine.snapshot(forPlayer = humanId)

        val humanView = snap.players.first { it.id == humanId }
        val bot1View  = snap.players.first { it.id == bot1Id  }
        val bot2View  = snap.players.first { it.id == bot2Id  }

        assertNotNull(humanView.holeCards, "Human must see own hole cards")
        assertNull(bot1View.holeCards,     "Human must NOT see Bot1 hole cards before Showdown")
        assertNull(bot2View.holeCards,     "Human must NOT see Bot2 hole cards before Showdown")
    }

    @Test
    fun `all hole cards are revealed at Showdown`() = runBlocking {
        val engine = buildEngine().playFullHand()
        val snap = engine.snapshot(forPlayer = humanId)

        snap.players.forEach { p ->
            assertNotNull(p.holeCards, "All hole cards must be visible at Showdown (player: ${p.name})")
            assertEquals(2, p.holeCards!!.size, "${p.name} must have exactly 2 hole cards")
        }
    }

    @Test
    fun `human hole cards are correct A and K of hearts`() = runBlocking {
        val engine = buildEngine()
        engine.startHand()

        val humanCards = engine.snapshot(forPlayer = humanId)
            .players.first { it.id == humanId }.holeCards!!

        val ranks = humanCards.map { it.rank }.toSet()
        val suits = humanCards.map { it.suit }.toSet()

        assertTrue(Rank.ACE  in ranks, "Human should hold an Ace")
        assertTrue(Rank.KING in ranks, "Human should hold a King")
        assertEquals(setOf(Suit.RED_HEART), suits, "Both hole cards should be RED_HEART")
    }

    // ── Chip accounting ───────────────────────────────────────────────────────

    @Test
    fun `total chips are conserved across the full hand`() = runBlocking {
        val engine = buildEngine().playFullHand()
        val total  = engine.snapshot().players.sumOf { it.coins }
        assertEquals(3_000, total, "Total chips must be conserved (3 × 1000 = 3000)")
    }

    @Test
    fun `human wins the pot and ends with more chips than started`() = runBlocking {
        val engine   = buildEngine().playFullHand()
        val snap     = engine.snapshot()
        val humanSnap = snap.players.first { it.id == humanId }

        // pot = 30 (all-in pre-flop: 10 each), human wins it all
        assertEquals(1_020, humanSnap.coins,
            "Human (Royal Flush winner) should receive the 30-chip pot: 1000 - 10 + 30 = 1020")
    }

    @Test
    fun `losing bots each spend only their blind or call amount`() = runBlocking {
        val engine = buildEngine().playFullHand()
        val snap   = engine.snapshot()

        val bot1Coins = snap.players.first { it.id == bot1Id }.coins
        val bot2Coins = snap.players.first { it.id == bot2Id }.coins

        assertEquals(990, bot1Coins, "Bot1 (SB → called total 10) should end at 990")
        assertEquals(990, bot2Coins, "Bot2 (BB → put in 10) should end at 990")
    }

    // ── Showdown ranking ──────────────────────────────────────────────────────

    @Test
    fun `showdown ranking is present and has all 3 players`() = runBlocking {
        val state = buildEngine().playFullHand().snapshot().state
        assertTrue(state is GameState.Showdown)
        assertEquals(3, (state as GameState.Showdown).ranking.size)
    }

    @Test
    fun `human ranks first in showdown with Royal Flush`() = runBlocking {
        val state    = buildEngine().playFullHand().snapshot().state as GameState.Showdown
        val topEntry = state.ranking.first()
        assertEquals(humanId, topEntry.playerId, "Human (Royal Flush) must rank first")
    }

    @Test
    fun `bots tie for second place with equal Straight hands`() = runBlocking {
        val state   = buildEngine().playFullHand().snapshot().state as GameState.Showdown
        val ranking = state.ranking

        // ranking[1] and ranking[2] should be the two bots — in any order
        val loserIds = ranking.drop(1).map { it.playerId }.toSet()
        assertEquals(setOf(bot1Id, bot2Id), loserIds, "Both bots must be ranked 2nd (tied Straights)")

        // Their hand values must be equal (same Straight over the board)
        val bot1Result = ranking.first { it.playerId == bot1Id }
        val bot2Result = ranking.first { it.playerId == bot2Id }
        assertEquals(0, bot1Result.compareTo(bot2Result), "Bots should tie (same best Straight)")
    }

    // ── toCall correctness ────────────────────────────────────────────────────

    @Test
    fun `toCall for UTG is BB amount (10) at start of hand`() = runBlocking {
        val engine = buildEngine()
        engine.startHand()

        val snap = engine.snapshot(forPlayer = humanId)
        assertEquals(10, snap.toCall, "UTG must call the full BB (10) pre-flop")
    }

    @Test
    fun `toCall is 0 for all players on the flop after equalization`() = runBlocking {
        val engine = buildEngine()
        engine.startHand()
        engine.applyAction(PlayerAction.Call(humanId))  // everyone equalises

        // Bots have already checked on the flop; now it's the human's turn
        val snap = engine.snapshot(forPlayer = humanId)
        assertEquals(0, snap.toCall, "No chips owed on the flop after all called pre-flop")
    }

    // ── Event emission ────────────────────────────────────────────────────────

    /**
     * Verifies that [GameEngine] emits exactly 13 [GameEvent.StateChanged] events
     * across a complete hand:
     *
     *   startHand                : 1  (PreFlop)
     *   human Call (pre-flop)    : 5  (human-call, bot1-call, bot2-check → Flop, bot1-check, bot2-check)
     *   human Check (flop)       : 3  (→ Turn, bot1-check, bot2-check)
     *   human Check (turn)       : 3  (→ River, bot1-check, bot2-check)
     *   human Check (river)      : 1  (→ Showdown)
     *   Total                    : 13
     */
    @Test
    fun `exactly 13 StateChanged events emitted across a full hand`() = runBlocking {
        val engine = buildEngine()
        val count  = AtomicInteger(0)

        // UNDISPATCHED: starts the collector synchronously so it subscribes to the
        // SharedFlow before startHand() emits the first event.  AtomicInteger.incrementAndGet()
        // never suspends, so the collector drains the entire SharedFlow buffer in one turn.
        val collectJob = launch(start = CoroutineStart.UNDISPATCHED) {
            engine.events.collect { if (it is GameEvent.StateChanged) count.incrementAndGet() }
        }

        engine.playFullHand()

        // yield() suspends the main coroutine for one scheduler tick, giving the collector
        // coroutine a turn to drain all events buffered in the SharedFlow (extraBufferCapacity=32).
        // In single-threaded runBlocking the collector never runs during playFullHand() because
        // the Mutex is always free and no suspension occurs — this yield() is the fix.
        yield()

        collectJob.cancel()
        assertEquals(13, count.get(), "Expected exactly 13 StateChanged events across the full hand")
    }

    // ── TableRepository + SessionRegistry (lightweight integration) ───────────

    @Test
    fun `TableRepository stores and retrieves a GameEngine by TableId`() {
        val repo    = TableRepository()
        val tableId = repo.create(blindAmount = 10)

        assertNotNull(repo.get(tableId), "Newly created table must be retrievable")
        assertEquals(1, repo.count())
    }

    @Test
    fun `SessionRegistry issues a token and validates it against the correct table`() {
        val repo     = TableRepository()
        val tableId  = repo.create()
        val registry = SessionRegistry()
        val playerId = PlayerId("player-1")

        val token   = registry.create(playerId, tableId)
        val session = registry.validate(token, tableId)

        assertNotNull(session, "Token must be valid for the table it was issued on")
        assertEquals(playerId, session!!.playerId)
        assertEquals(tableId,  session.tableId)
    }

    @Test
    fun `SessionRegistry rejects a token used against a different table`() {
        val repo      = TableRepository()
        val tableId1  = repo.create()
        val tableId2  = repo.create()
        val registry  = SessionRegistry()
        val token     = registry.create(PlayerId("p"), tableId1)

        assertNull(registry.validate(token, tableId2),
            "Token issued for table1 must be rejected when validated against table2")
    }

    @Test
    fun `SessionRegistry revoke removes the session`() {
        val repo     = TableRepository()
        val tableId  = repo.create()
        val registry = SessionRegistry()
        val token    = registry.create(PlayerId("p"), tableId)

        registry.revoke(token)
        assertNull(registry.get(token), "Revoked token must not be retrievable")
    }
}
