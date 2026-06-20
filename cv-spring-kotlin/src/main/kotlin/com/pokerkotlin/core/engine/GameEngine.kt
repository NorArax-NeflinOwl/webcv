package com.pokerkotlin.core.engine

import com.pokerkotlin.core.evaluation.PlayerResult
import com.pokerkotlin.core.model.*
import com.pokerkotlin.core.strategy.BotStrategy
import java.security.SecureRandom
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.SharedFlow
import kotlinx.coroutines.flow.asSharedFlow
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import org.slf4j.LoggerFactory

class GameEngine(
    val tableId: TableId,
    private val blindAmount: Int = 10,
    private val random: SecureRandom = SecureRandom(),
    /** Deck factory — overridable in tests to inject a deterministic card order. */
    private val deckFactory: () -> Deck = { Deck.shuffle(random) },
) {
    // ─── Internal seat representation ────────────────────────────────────────
    private data class Seat(
        val player: Player,
        /** null = human player; non-null = bot using the given strategy */
        val strategy: BotStrategy?,
    )

    private val log = LoggerFactory.getLogger(javaClass)
    private val seats = mutableListOf<Seat>()
    private val mutex = Mutex()

    private var gameState: GameState = GameState.WaitingForPlayers
    private var deck = Deck.shuffle(random)
    private var handPlayers: List<Player> = emptyList()
    private var board: List<Card> = emptyList()
    private var pot: Int = 0
    private var dealerIndex: Int = 0      // rotates after each hand
    private var currentBet: Int = 0
    private var betsThisRound: MutableMap<PlayerId, Int> = mutableMapOf()

    /**
     * Queue of players waiting to act in the current betting round.
     * The player at index 0 holds the current turn.
     */
    private var actionQueue: ArrayDeque<PlayerId> = ArrayDeque()

    // ─── SharedFlow (Observer) ────────────────────────────────────────────────
    private val _events = MutableSharedFlow<GameEvent>(extraBufferCapacity = 32)
    val events: SharedFlow<GameEvent> = _events.asSharedFlow()

    // ─────────────────────────────────────────────────────────────────────────
    // Seat management
    // ─────────────────────────────────────────────────────────────────────────

    fun addPlayer(player: Player, strategy: BotStrategy? = null) {
        require(seats.none { it.player.id == player.id }) {
            "Player ${player.name} is already seated at this table"
        }
        seats.add(Seat(player, strategy))
        if (log.isInfoEnabled) {
            val type = if (strategy != null) "bot[${strategy::class.simpleName}]" else "human"
            log.info("[{}] Player joined: {} ({}) | coins={}", tableId.value, player.name, type, player.coins)
        }
    }

    fun removePlayer(playerId: PlayerId) {
        val removed = seats.find { it.player.id == playerId }
        seats.removeIf { it.player.id == playerId }
        if (removed != null) {
            log.info("[{}] Player left: {}", tableId.value, removed.player.name)
        } else {
            log.warn("[{}] removePlayer: player not found [playerId={}]", tableId.value, playerId.value)
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Game flow — public API
    // ─────────────────────────────────────────────────────────────────────────

    /**
     * Starts a new hand: shuffles the deck, deals hole cards, posts blinds,
     * builds the action queue, then auto-plays bots until a human turn is reached.
     */
    suspend fun startHand() = mutex.withLock {
        require(seats.size >= 2) { "At least 2 players are required to start a hand" }
        require(gameState is GameState.WaitingForPlayers || gameState is GameState.Showdown) {
            "Cannot start a new hand while a round is in progress"
        }

        deck = deckFactory()
        board = emptyList()
        pot = 0
        currentBet = 0
        betsThisRound = mutableMapOf()

        val activeSeatPlayers = seats.filter { it.player.coins > 0 }
        require(activeSeatPlayers.size >= 2) { "Not enough players with chips" }

        // Deal 2 hole cards to each active player
        handPlayers = activeSeatPlayers.map { seat ->
            seat.player.copy(holeCards = Hand(deck.drawMany(2)), folded = false)
        }

        // Blinds: SB = player after dealer, BB = player after SB
        val n = handPlayers.size
        val sbIndex = (dealerIndex + 1) % n
        val bbIndex = (dealerIndex + 2) % n

        postBlind(handPlayers[sbIndex].id, blindAmount / 2)
        postBlind(handPlayers[bbIndex].id, blindAmount)
        currentBet = blindAmount

        // Pre-flop: action starts from the player after BB (UTG)
        val firstActIndex = (bbIndex + 1) % n
        actionQueue = buildActionQueue(startIndex = firstActIndex)

        if (log.isInfoEnabled) {
            log.info(
                "[{}] ── New hand ── players={} SB={} BB={} blind={}",
                tableId.value,
                handPlayers.map { it.name },
                handPlayers[sbIndex].name,
                handPlayers[bbIndex].name,
                blindAmount,
            )
        }

        gameState = GameState.PreFlop(pot)
        publish()

        // Auto-play bots until a human player's turn
        processBotActionsInternal()
    }

    /**
     * Accepts and validates a human player's action, then auto-plays bots
     * until the next human turn or end of round.
     *
     * @throws IllegalStateException if it is not this player's turn, or the action is invalid.
     */
    suspend fun applyAction(action: PlayerAction) = mutex.withLock {
        check(gameState !is GameState.WaitingForPlayers && gameState !is GameState.Showdown) {
            "No active betting round"
        }
        applyActionInternal(action)
        processBotActionsInternal()
    }

    /**
     * Returns a table snapshot tailored to a specific player.
     * Opponents' hole cards are hidden (null) until Showdown.
     *
     * @param forPlayer The requesting player's ID; null for an observer (no hole cards).
     */
    fun snapshot(forPlayer: PlayerId? = null): TableView = TableView(
        state = gameState,
        board = board,
        pot = pot,
        toCall = forPlayer?.let { toCall(it) } ?: 0,
        players = handPlayers.map { p ->
            PlayerView(
                id = p.id,
                name = p.name,
                coins = p.coins,
                folded = p.folded,
                betThisRound = betsThisRound[p.id] ?: 0,
                holeCards = when {
                    p.id == forPlayer            -> p.holeCards.cards()  // own cards — always visible
                    gameState is GameState.Showdown -> p.holeCards.cards()  // showdown — reveal all
                    else                         -> null                 // opponent cards — hidden
                },
            )
        },
        currentTurnId = actionQueue.firstOrNull(),
    )

    // ─────────────────────────────────────────────────────────────────────────
    // Action logic (private)
    // ─────────────────────────────────────────────────────────────────────────

    private fun applyActionInternal(action: PlayerAction) {
        // Validate turn order
        val currentTurn = actionQueue.firstOrNull()
            ?: error("No player is waiting to act")
        check(action.playerId == currentTurn) {
            "Not player ${action.playerId.value}'s turn (current turn: ${currentTurn.value})"
        }

        val player = requireActivePlayer(action.playerId)

        when (action) {
            is PlayerAction.Fold -> {
                log.debug("[{}] {} FOLD | phase={}", tableId.value, player.name, gameState::class.simpleName)
                handPlayers = handPlayers.map {
                    if (it.id == action.playerId) it.copy(folded = true) else it
                }
                actionQueue.removeFirst()

                // If only one player remains active, they win the pot immediately
                val remaining = activePlayers()
                if (remaining.size == 1) {
                    log.info("[{}] All others folded — {} wins | pot={}", tableId.value, remaining.first().name, pot)
                    awardPot(listOf(remaining.first().id))
                    finishHand(emptyList())
                    return
                }
            }

            is PlayerAction.Check -> {
                val owed = toCall(action.playerId)
                check(owed == 0) {
                    "Cannot check — player must call $owed chips or fold"
                }
                log.debug("[{}] {} CHECK | phase={}", tableId.value, player.name, gameState::class.simpleName)
                actionQueue.removeFirst()
            }

            is PlayerAction.Call -> {
                // Match currentBet; if not enough chips, go all-in
                val owed = minOf(toCall(action.playerId), player.coins)
                log.debug("[{}] {} CALL {} | pot={} phase={}", tableId.value, player.name, owed, pot + owed, gameState::class.simpleName)
                placeBet(action.playerId, owed)
                actionQueue.removeFirst()
            }

            is PlayerAction.Raise -> {
                val totalContribution = (betsThisRound[action.playerId] ?: 0) + action.amount
                require(totalContribution > currentBet) {
                    "Raise total ($totalContribution) must exceed the current bet ($currentBet)"
                }
                require(action.amount <= player.coins) {
                    "Not enough chips: ${player.coins} < ${action.amount}"
                }
                log.debug(
                    "[{}] {} RAISE {} (total={}) | pot={} phase={}",
                    tableId.value, player.name, action.amount, totalContribution,
                    pot + action.amount, gameState::class.simpleName,
                )
                placeBet(action.playerId, action.amount)
                currentBet = betsThisRound[action.playerId]!!
                // All remaining active players must act again after a raise
                rebuildQueueAfterRaise(raiserId = action.playerId)
            }
        }

        // Betting round ends when the action queue is empty
        if (actionQueue.isEmpty()) {
            advancePhase()
        } else {
            publish()
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Phase transitions
    // ─────────────────────────────────────────────────────────────────────────

    private fun advancePhase() {
        currentBet = 0
        betsThisRound.clear()

        gameState = when (gameState) {
            is GameState.PreFlop -> {
                board = deck.drawMany(3)
                resetPostFlopQueue()
                log.info("[{}] ── FLOP: {} | pot={}", tableId.value, board, pot)
                GameState.Flop(pot, board)
            }
            is GameState.Flop -> {
                board = board + deck.draw()
                resetPostFlopQueue()
                log.info("[{}] ── TURN: {} | pot={}", tableId.value, board.last(), pot)
                GameState.Turn(pot, board)
            }
            is GameState.Turn -> {
                board = board + deck.draw()
                resetPostFlopQueue()
                log.info("[{}] ── RIVER: {} | pot={}", tableId.value, board.last(), pot)
                GameState.River(pot, board)
            }
            is GameState.River -> {
                val ranking = evaluateShowdown()
                val winners = findWinners(ranking)
                if (winners.size == 1) {
                    log.info("[{}] ── SHOWDOWN | winner={} | ranking={}", tableId.value, ranking.first().playerName, ranking.map { it.playerName })
                } else {
                    log.info("[{}] ── SHOWDOWN tie | winners={} | ranking={}", tableId.value, winners.map { it.value }, ranking.map { it.playerName })
                }
                awardPot(winners)
                finishHand(ranking)
                return  // finishHand sets gameState and calls publish()
            }
            else -> error("Cannot advance phase from: $gameState")
        }

        publish()
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Bot auto-play
    // ─────────────────────────────────────────────────────────────────────────

    /**
     * Processes bot actions in sequence until:
     *  - the action queue is empty (round over or new phase started), or
     *  - the next player in the queue is a human, or
     *  - the game reaches Showdown.
     */
    private fun processBotActionsInternal() {
        while (true) {
            if (gameState is GameState.Showdown || gameState is GameState.WaitingForPlayers) break
            val nextId = actionQueue.firstOrNull() ?: break
            val seat = seats.firstOrNull { it.player.id == nextId } ?: break
            val strategy = seat.strategy ?: break   // human player — wait for applyAction

            val view = snapshot(forPlayer = nextId)
            val playerView = view.players.first { it.id == nextId }
            val action = strategy.decide(view, playerView)
            applyActionInternal(action)
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers (private)
    // ─────────────────────────────────────────────────────────────────────────

    private fun activePlayers(): List<Player> = handPlayers.filter { !it.folded }

    private fun requireActivePlayer(id: PlayerId): Player =
        handPlayers.firstOrNull { it.id == id && !it.folded }
            ?: throw IllegalStateException("Player ${id.value} is not active or has folded")

    private fun toCall(playerId: PlayerId): Int =
        maxOf(0, currentBet - (betsThisRound[playerId] ?: 0))

    /** Posts a forced blind bet (no action validation applied). */
    private fun postBlind(playerId: PlayerId, amount: Int) {
        val actual = minOf(amount, handPlayers.first { it.id == playerId }.coins)
        updatePlayerCoins(playerId, -actual)
        betsThisRound[playerId] = (betsThisRound[playerId] ?: 0) + actual
        pot += actual
    }

    /** Adds [amount] chips to the pot from player [playerId]. */
    private fun placeBet(playerId: PlayerId, amount: Int) {
        updatePlayerCoins(playerId, -amount)
        betsThisRound[playerId] = (betsThisRound[playerId] ?: 0) + amount
        pot += amount
    }

    /** Updates a player's chip count in [handPlayers] via immutable copy. */
    private fun updatePlayerCoins(playerId: PlayerId, delta: Int) {
        handPlayers = handPlayers.map { p ->
            if (p.id == playerId) p.copy(coins = p.coins + delta) else p
        }
    }

    /**
     * Builds an action queue starting at [startIndex] (inclusive),
     * skipping folded players.
     */
    private fun buildActionQueue(startIndex: Int): ArrayDeque<PlayerId> {
        val n = handPlayers.size
        val order = (startIndex until startIndex + n)
            .map { handPlayers[it % n] }
            .filterNot { it.folded }
            .map { it.id }
        return ArrayDeque(order)
    }

    /** Post-flop: the first active player left of the dealer acts first. */
    private fun resetPostFlopQueue() {
        val startIndex = (dealerIndex + 1) % handPlayers.size
        actionQueue = buildActionQueue(startIndex)
    }

    /**
     * Rebuilds the action queue after a raise, starting from the player
     * after [raiserId]. The raiser is excluded (they do not re-act on their own raise).
     */
    private fun rebuildQueueAfterRaise(raiserId: PlayerId) {
        val raiserIndex = handPlayers.indexOfFirst { it.id == raiserId }
        val nextIndex = (raiserIndex + 1) % handPlayers.size
        actionQueue = buildActionQueue(startIndex = nextIndex).also { q ->
            q.removeAll { it == raiserId }
        }
    }

    /**
     * Evaluates each active player's best hand (hole cards + board),
     * sorted descending (strongest first).
     */
    private fun evaluateShowdown(): List<PlayerResult> =
        activePlayers()
            .map { p ->
                PlayerResult(
                    playerId = p.id,
                    playerName = p.name,
                    handValue = Hand(p.holeCards.cards() + board),
                )
            }
            .sortedDescending()

    /**
     * Splits the pot evenly among [winnerIds].
     * Any remainder (when the pot is not evenly divisible) goes to the first player in the list.
     */
    private fun awardPot(winnerIds: List<PlayerId>) {
        val n = winnerIds.size
        if (n == 0) return
        val share = pot / n
        val remainder = pot % n
        winnerIds.forEachIndexed { i, winnerId ->
            val amount = share + if (i == 0) remainder else 0
            val name = handPlayers.firstOrNull { it.id == winnerId }?.name ?: winnerId.value
            log.info("[{}] {} receives {} from pot", tableId.value, name, amount)
            updatePlayerCoins(winnerId, amount)
        }
        // Sync updated coin counts back to seats for the next hand
        seats.replaceAll { seat ->
            val updated = handPlayers.firstOrNull { it.id == seat.player.id }
            if (updated != null) seat.copy(player = seat.player.copy(coins = updated.coins))
            else seat
        }
        pot = 0
    }

    /**
     * Returns the [PlayerId]s of all players whose hand equals the strongest hand in [ranking].
     * [ranking] must be sorted descending (strongest first).
     */
    private fun findWinners(ranking: List<PlayerResult>): List<PlayerId> {
        if (ranking.isEmpty()) return emptyList()
        val best = ranking.first()
        return ranking
            .takeWhile { it.compareTo(best) == 0 }
            .map { it.playerId }
    }

    /** Sets the final hand state and advances the dealer position. */
    private fun finishHand(ranking: List<PlayerResult>) {
        dealerIndex = (dealerIndex + 1) % seats.size
        actionQueue.clear()
        gameState = GameState.Showdown(pot, board, ranking)
        publish()
    }

    private fun publish() {
        _events.tryEmit(GameEvent.StateChanged(tableId))
    }
}
