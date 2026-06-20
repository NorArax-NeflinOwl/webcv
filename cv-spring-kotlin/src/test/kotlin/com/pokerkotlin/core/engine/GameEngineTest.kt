package com.pokerkotlin.core.engine

import com.pokerkotlin.core.model.*
import kotlinx.coroutines.runBlocking
import org.junit.jupiter.api.Assertions.*
import org.junit.jupiter.api.BeforeEach
import org.junit.jupiter.api.Test

class GameEngineTest {

    // ── Helpers ──────────────────────────────────────────────

    private fun player(id: String, name: String, coins: Int = 1000) = Player(
        id        = PlayerId(id),
        name      = name,
        coins     = coins,
        holeCards = Hand(emptyList()),
    )

    private fun card(rank: Rank, suit: Suit) = Card(rank, suit)

    /**
     * Builds a deterministic deck for a 2-player game.
     *
     * Deal order with 2 players (dealer=0, SB=p1, BB=p0):
     *   handPlayers[0] = p0 → deck[0], deck[1]
     *   handPlayers[1] = p1 → deck[2], deck[3]
     *   flop            → deck[4], deck[5], deck[6]
     *   turn            → deck[7]
     *   river           → deck[8]
     */
    private fun deck2(
        p0c1: Card, p0c2: Card,
        p1c1: Card, p1c2: Card,
        f1: Card, f2: Card, f3: Card,
        turn: Card,
        river: Card,
    ) = Deck.of(p0c1, p0c2, p1c1, p1c2, f1, f2, f3, turn, river)

    private val p0 = player("p0", "Alice")
    private val p1 = player("p1", "Bob")

    private lateinit var engine: GameEngine

    @BeforeEach
    fun setUp() {
        engine = GameEngine(tableId = TableId("test"), blindAmount = 10)
    }

    // ── startHand ────────────────────────────────────────────

    @Test
    fun `startHand requires at least 2 players`() {
        engine.addPlayer(p0)
        assertThrows(IllegalArgumentException::class.java) {
            runBlocking { engine.startHand() }
        }
    }

    @Test
    fun `startHand sets PreFlop state`() = runBlocking {
        engine.addPlayer(p0)
        engine.addPlayer(p1)
        engine.startHand()
        assertTrue(engine.snapshot().state is GameState.PreFlop)
    }

    @Test
    fun `snapshot returns currentTurnId after startHand`() = runBlocking {
        engine.addPlayer(p0)
        engine.addPlayer(p1)
        engine.startHand()
        assertNotNull(engine.snapshot().currentTurnId)
    }

    @Test
    fun `snapshot hides opponent hole cards before showdown`() = runBlocking {
        engine.addPlayer(p0)
        engine.addPlayer(p1)
        engine.startHand()

        val snap  = engine.snapshot(forPlayer = PlayerId("p0"))
        val p0view = snap.players.first { it.id == PlayerId("p0") }
        val p1view = snap.players.first { it.id == PlayerId("p1") }

        assertNotNull(p0view.holeCards)  // own cards visible
        assertNull(p1view.holeCards)     // opponent cards hidden
    }

    // ── Action validation ────────────────────────────────────

    @Test
    fun `action out of turn throws`() = runBlocking {
        engine.addPlayer(p0)
        engine.addPlayer(p1)
        engine.startHand()

        val firstToAct = engine.snapshot().currentTurnId!!
        val notMyTurn  = if (firstToAct == PlayerId("p0")) PlayerId("p1") else PlayerId("p0")

        assertThrows(IllegalStateException::class.java) {
            runBlocking { engine.applyAction(PlayerAction.Check(notMyTurn)) }
        }
    }

    @Test
    fun `check is illegal when player owes chips`() = runBlocking {
        engine.addPlayer(p0)
        engine.addPlayer(p1)
        engine.startHand()

        // SB posted 5, currentBet=10 — must call 5 more, cannot check
        val firstToAct = engine.snapshot().currentTurnId!!
        val toCall     = engine.snapshot(firstToAct).toCall

        if (toCall > 0) {
            assertThrows(IllegalStateException::class.java) {
                runBlocking { engine.applyAction(PlayerAction.Check(firstToAct)) }
            }
        }
    }

    // ── Fold ─────────────────────────────────────────────────

    @Test
    fun `fold — pot goes to the only remaining player`() = runBlocking {
        engine = GameEngine(
            tableId     = TableId("test"),
            blindAmount = 10,
            deckFactory = {
                deck2(
                    card(Rank.TWO, Suit.GREEN_CLUB),    card(Rank.THREE, Suit.GREEN_CLUB),
                    card(Rank.FOUR, Suit.BLUE_DIAMOND), card(Rank.FIVE, Suit.BLUE_DIAMOND),
                    card(Rank.SIX, Suit.RED_HEART), card(Rank.SEVEN, Suit.RED_HEART), card(Rank.EIGHT, Suit.RED_HEART),
                    card(Rank.NINE, Suit.RED_HEART),
                    card(Rank.TEN, Suit.RED_HEART),
                )
            },
        )
        engine.addPlayer(p0)
        engine.addPlayer(p1)
        engine.startHand()

        val firstToAct = engine.snapshot().currentTurnId!!
        engine.applyAction(PlayerAction.Fold(firstToAct))

        val finalSnap = engine.snapshot()
        assertTrue(finalSnap.state is GameState.Showdown)

        // Folder loses, opponent wins the pot of 15 (SB=5 + BB=10)
        val winner = finalSnap.players.first { it.id != firstToAct }
        val loser  = finalSnap.players.first { it.id == firstToAct }
        assertTrue(winner.coins > 1000, "Winner should have more than the starting 1000")
        assertTrue(loser.coins  < 1000, "Folder should have less than the starting 1000")
    }

    @Test
    fun `fold — total chips are conserved`() = runBlocking {
        engine.addPlayer(p0)
        engine.addPlayer(p1)
        engine.startHand()

        val firstToAct = engine.snapshot().currentTurnId!!
        engine.applyAction(PlayerAction.Fold(firstToAct))

        assertEquals(2000, engine.snapshot().players.sumOf { it.coins },
            "Total chips must be conserved after fold")
    }

    // ── Full hand to showdown ────────────────────────────────

    @Test
    fun `full hand — showdown reached after call and checks, chips conserved`() = runBlocking {
        engine = GameEngine(
            tableId     = TableId("test"),
            blindAmount = 10,
            deckFactory = {
                deck2(
                    card(Rank.ACE, Suit.RED_HEART),     card(Rank.KING, Suit.RED_HEART),
                    card(Rank.TWO, Suit.GREEN_CLUB),    card(Rank.THREE, Suit.GREEN_CLUB),
                    card(Rank.FOUR, Suit.BLUE_DIAMOND), card(Rank.FIVE, Suit.BLUE_DIAMOND), card(Rank.SEVEN, Suit.BLACK_SPADE),
                    card(Rank.EIGHT, Suit.BLACK_SPADE),
                    card(Rank.NINE, Suit.BLACK_SPADE),
                )
            },
        )
        engine.addPlayer(p0)
        engine.addPlayer(p1)
        engine.startHand()

        // Pre-flop: SB calls, BB checks
        engine.applyAction(PlayerAction.Call(engine.snapshot().currentTurnId!!))
        engine.applyAction(PlayerAction.Check(engine.snapshot().currentTurnId!!))

        // Flop, Turn, River: both check
        repeat(6) {
            val turn = engine.snapshot().currentTurnId
            if (turn != null) engine.applyAction(PlayerAction.Check(turn))
        }

        val finalSnap = engine.snapshot()
        assertTrue(finalSnap.state is GameState.Showdown)
        assertEquals(2000, finalSnap.players.sumOf { it.coins })
    }

    // ── Tie (split pot) ──────────────────────────────────────

    @Test
    fun `tie — pot split evenly, both players return to starting chips`() = runBlocking {
        // Both hole cards are irrelevant; board = A-A-A-A-K → Four Aces + King for both
        engine = GameEngine(
            tableId     = TableId("test"),
            blindAmount = 10,
            deckFactory = {
                deck2(
                    card(Rank.TWO, Suit.GREEN_CLUB),    card(Rank.THREE, Suit.BLACK_SPADE),
                    card(Rank.FOUR, Suit.BLUE_DIAMOND), card(Rank.FIVE, Suit.RED_HEART),
                    card(Rank.ACE, Suit.RED_HEART),    card(Rank.ACE, Suit.BLACK_SPADE),    card(Rank.ACE, Suit.GREEN_CLUB),
                    card(Rank.ACE, Suit.BLUE_DIAMOND),
                    card(Rank.KING, Suit.RED_HEART),
                )
            },
        )
        engine.addPlayer(p0)
        engine.addPlayer(p1)
        engine.startHand()

        // Pre-flop: SB (p1) calls, BB (p0) checks → pot = 20
        engine.applyAction(PlayerAction.Call(engine.snapshot().currentTurnId!!))
        engine.applyAction(PlayerAction.Check(engine.snapshot().currentTurnId!!))

        // Flop, Turn, River: both check
        repeat(6) {
            val turn = engine.snapshot().currentTurnId
            if (turn != null) engine.applyAction(PlayerAction.Check(turn))
        }

        val finalSnap = engine.snapshot()
        assertTrue(finalSnap.state is GameState.Showdown, "Expected Showdown state")

        val p0coins = finalSnap.players.first { it.id == PlayerId("p0") }.coins
        val p1coins = finalSnap.players.first { it.id == PlayerId("p1") }.coins

        // pot=20, split → 10 each. Both invested 10 → back to 1000.
        assertEquals(1000, p0coins, "Alice should return to 1000 after a tie")
        assertEquals(1000, p1coins, "Bob should return to 1000 after a tie")
    }

    @Test
    fun `tie — total chips are conserved`() = runBlocking {
        engine = GameEngine(
            tableId     = TableId("test"),
            blindAmount = 10,
            deckFactory = {
                deck2(
                    card(Rank.TWO, Suit.GREEN_CLUB),    card(Rank.THREE, Suit.BLACK_SPADE),
                    card(Rank.FOUR, Suit.BLUE_DIAMOND), card(Rank.FIVE, Suit.RED_HEART),
                    card(Rank.ACE, Suit.RED_HEART),    card(Rank.ACE, Suit.BLACK_SPADE),    card(Rank.ACE, Suit.GREEN_CLUB),
                    card(Rank.ACE, Suit.BLUE_DIAMOND),
                    card(Rank.KING, Suit.RED_HEART),
                )
            },
        )
        engine.addPlayer(p0)
        engine.addPlayer(p1)
        engine.startHand()

        engine.applyAction(PlayerAction.Call(engine.snapshot().currentTurnId!!))
        engine.applyAction(PlayerAction.Check(engine.snapshot().currentTurnId!!))
        repeat(6) {
            val turn = engine.snapshot().currentTurnId
            if (turn != null) engine.applyAction(PlayerAction.Check(turn))
        }

        assertEquals(2000, engine.snapshot().players.sumOf { it.coins },
            "Total chips must be conserved after a split pot")
    }

    // ── Raise ────────────────────────────────────────────────

    @Test
    fun `raise — opponent must respond`() = runBlocking {
        engine.addPlayer(p0)
        engine.addPlayer(p1)
        engine.startHand()

        val sbId = engine.snapshot().currentTurnId!!
        engine.applyAction(PlayerAction.Raise(sbId, amount = 50))

        val nextTurn = engine.snapshot().currentTurnId
        assertNotNull(nextTurn)
        assertNotEquals(sbId, nextTurn, "After a raise it should not be the raiser's turn again")
    }

    @Test
    fun `raise — check after raise is illegal`() = runBlocking {
        engine.addPlayer(p0)
        engine.addPlayer(p1)
        engine.startHand()

        val sbId = engine.snapshot().currentTurnId!!
        engine.applyAction(PlayerAction.Raise(sbId, amount = 50))

        // BB must call or fold, cannot check
        val bbId = engine.snapshot().currentTurnId!!
        assertThrows(IllegalStateException::class.java) {
            runBlocking { engine.applyAction(PlayerAction.Check(bbId)) }
        }
    }
}
