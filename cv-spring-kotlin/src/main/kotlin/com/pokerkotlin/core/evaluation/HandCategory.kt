package com.pokerkotlin.core.evaluation

/**
 * Poker hand rankings ordered from lowest (HIGH_CARD) to highest (ROYAL_FLUSH).
 * Ordinal is used directly for comparison — do not reorder entries.
 */
enum class HandCategory(val displayName: String) {
    HIGH_CARD("High Card"),
    ONE_PAIR("One Pair"),
    TWO_PAIR("Two Pair"),
    THREE_OF_A_KIND("Three of a Kind"),
    STRAIGHT("Straight"),
    FLUSH("Flush"),
    FULL_HOUSE("Full House"),
    FOUR_OF_A_KIND("Four of a Kind"),
    STRAIGHT_FLUSH("Straight Flush"),
    ROYAL_FLUSH("Royal Flush"),
}
