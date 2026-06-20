package com.pokerkotlin.core.evaluation

import com.pokerkotlin.core.model.Hand
import com.pokerkotlin.core.model.PlayerId

/**
 * The outcome of evaluating a player's hand at showdown.
 *
 * [handValue] holds the full evaluation hand (hole cards + board) so that
 * [compareTo] can delegate to [Hand.compareTo] → [HandEvaluator].
 * Sorted descending — the strongest hand ranks first.
 */
data class PlayerResult(
    val playerId: PlayerId,
    val playerName: String,
    val handValue: Hand,
) : Comparable<PlayerResult> {
    override fun compareTo(other: PlayerResult): Int = handValue.compareTo(other.handValue)
}
