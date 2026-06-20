package com.pokerkotlin.core.evaluation

/**
 * The evaluated rank of a 5-card poker hand.
 *
 * @property category  The hand category (pair, flush, etc.).
 * @property tiebreakers  Card values used to break ties within the same category,
 *                        ordered from most significant to least (e.g. pair rank first,
 *                        then kickers in descending order).
 */
data class HandRank(
    val category: HandCategory,
    val tiebreakers: List<Int>,
) : Comparable<HandRank> {

    /**
     * Compares first by category ordinal, then by tiebreakers left-to-right.
     * Returns positive if this hand is stronger.
     */
    override fun compareTo(other: HandRank): Int {
        val categoryDiff = category.ordinal - other.category.ordinal
        if (categoryDiff != 0) return categoryDiff

        for ((a, b) in tiebreakers.zip(other.tiebreakers)) {
            val diff = a - b
            if (diff != 0) return diff
        }
        return 0
    }

    override fun toString(): String = "${category.displayName} $tiebreakers"
}
