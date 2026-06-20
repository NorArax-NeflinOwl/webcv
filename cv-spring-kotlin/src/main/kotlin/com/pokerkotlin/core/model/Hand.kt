package com.pokerkotlin.core.model

import com.pokerkotlin.core.evaluation.HandEvaluator

/**
 * Represents a set of cards — either a player's hole cards (2 cards)
 * or a full evaluation hand (5–7 cards: hole cards + board).
 *
 * [compareTo] is only valid for evaluation hands (5–7 cards) and delegates
 * to [HandEvaluator]. Do not call compareTo on 2-card hole hands.
 */
class Hand(private val cards: List<Card>) : Comparable<Hand> {

    fun cards(): List<Card> = cards

    override fun compareTo(other: Hand): Int =
        HandEvaluator.evaluate(this).compareTo(HandEvaluator.evaluate(other))

    override fun toString(): String = cards.joinToString(", ", "[", "]")
}
