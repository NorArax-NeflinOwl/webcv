package com.pokerkotlin.server.dto

import com.fasterxml.jackson.annotation.JsonSubTypes
import com.fasterxml.jackson.annotation.JsonTypeInfo
import com.pokerkotlin.core.engine.GameState
import com.pokerkotlin.core.engine.PlayerAction
import com.pokerkotlin.core.engine.TableView
import com.pokerkotlin.core.evaluation.HandEvaluator
import com.pokerkotlin.core.model.PlayerId

// ── Outbound (server → client): snapshot sent over WebSocket ─────────────────

data class TableSnapshotDto(
    /** Discriminator string: WaitingForPlayers | PreFlop | Flop | Turn | River | Showdown */
    val phase: String,
    val pot: Int,
    /** How much the requesting player must pay to call. */
    val toCall: Int,
    val board: List<CardDto>,
    val players: List<PlayerViewDto>,
    /** PlayerId.value of the player whose turn it is, or null when no betting is ongoing. */
    val currentTurnId: String?,
    /** Only present in Showdown — ranked list of player results. */
    val ranking: List<ShowdownResultDto>?,
)

data class CardDto(val rank: String, val suit: String)

data class PlayerViewDto(
    val id: String,
    val name: String,
    val coins: Int,
    val folded: Boolean,
    val betThisRound: Int,
    /** null means the cards are face-down (opponent in a non-showdown phase). */
    val holeCards: List<CardDto>?,
)

data class ShowdownResultDto(val playerId: String, val playerName: String, val handCategory: String)

// ── Extension: TableView → TableSnapshotDto ───────────────────────────────────

fun TableView.toDto(): TableSnapshotDto {
    val phase = when (state) {
        is GameState.WaitingForPlayers -> "WaitingForPlayers"
        is GameState.PreFlop           -> "PreFlop"
        is GameState.Flop              -> "Flop"
        is GameState.Turn              -> "Turn"
        is GameState.River             -> "River"
        is GameState.Showdown          -> "Showdown"
    }

    val ranking = if (state is GameState.Showdown) {
        state.ranking.map {
            ShowdownResultDto(
                playerId = it.playerId.value,
                playerName = it.playerName,
                handCategory = HandEvaluator.evaluate(it.handValue).category.displayName,
            )
        }
    } else null

    return TableSnapshotDto(
        phase = phase,
        pot = pot,
        toCall = toCall,
        board = board.map { CardDto(it.rank.name, it.suit.name) },
        players = players.map { p ->
            PlayerViewDto(
                id = p.id.value,
                name = p.name,
                coins = p.coins,
                folded = p.folded,
                betThisRound = p.betThisRound,
                holeCards = p.holeCards?.map { CardDto(it.rank.name, it.suit.name) },
            )
        },
        currentTurnId = currentTurnId?.value,
        ranking = ranking,
    )
}

// ── Inbound (client → server): action sent over WebSocket ────────────────────

/**
 * Polymorphic JSON with a "type" discriminator field.
 *
 * Example payloads:
 *   {"type":"Fold"}
 *   {"type":"Check"}
 *   {"type":"Call"}
 *   {"type":"Raise","amount":50}
 *
 * The `playerId` is NOT taken from the JSON payload; it is always
 * injected from the authenticated session on the server side.
 */
@JsonTypeInfo(use = JsonTypeInfo.Id.NAME, property = "type")
@JsonSubTypes(
    JsonSubTypes.Type(ActionRequest.Fold::class,  name = "Fold"),
    JsonSubTypes.Type(ActionRequest.Check::class, name = "Check"),
    JsonSubTypes.Type(ActionRequest.Call::class,  name = "Call"),
    JsonSubTypes.Type(ActionRequest.Raise::class, name = "Raise"),
)
sealed interface ActionRequest {
    data object Fold  : ActionRequest
    data object Check : ActionRequest
    data object Call  : ActionRequest
    data class  Raise(val amount: Int) : ActionRequest
}

/** Maps an [ActionRequest] (DTO) to a domain [PlayerAction]. */
fun ActionRequest.toDomain(playerId: PlayerId): PlayerAction = when (this) {
    is ActionRequest.Fold  -> PlayerAction.Fold(playerId)
    is ActionRequest.Check -> PlayerAction.Check(playerId)
    is ActionRequest.Call  -> PlayerAction.Call(playerId)
    is ActionRequest.Raise -> PlayerAction.Raise(playerId, amount)
}
