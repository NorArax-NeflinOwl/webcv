package com.pokerkotlin.core.evaluation

import com.pokerkotlin.core.model.Hand
import org.slf4j.LoggerFactory

/**
 * Evaluates the best 5-card Texas Hold'em hand from a [Hand] containing 5–7 cards.
 *
 * Algorithm:
 *  1. Generate all C(n, 5) five-card combinations (max 21 for 7 cards).
 *  2. Score each combination via [evaluateFive].
 *  3. Return the highest-scoring [HandRank].
 *
 * [evaluateFive] detects hand category using rank-group sizes and suit/straight checks,
 * then builds an ordered tiebreaker list for within-category comparison.
 */
object HandEvaluator {

    private val log = LoggerFactory.getLogger(HandEvaluator::class.java)

    // ─────────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────────

    /**
     * Evaluates the best possible 5-card hand from [hand].
     *
     * @param hand A [Hand] with 5–7 cards (2 hole + up to 5 board cards).
     * @return The [HandRank] of the best 5-card combination.
     * @throws IllegalArgumentException if the hand does not contain 5–7 cards.
     */
    fun evaluate(hand: Hand): HandRank {
        val cards = hand.cards()
        require(cards.size in 5..7) {
            "HandEvaluator requires 5–7 cards, got ${cards.size}"
        }

        val best = combinations(cards, 5)
            .map { evaluateFive(it) }
            .max()

        if (log.isDebugEnabled) {
            log.debug(
                "Evaluated {}-card hand -> {} | tiebreakers={}",
                cards.size, best.category.displayName, best.tiebreakers,
            )
        }
        return best
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Five-card evaluation
    // ─────────────────────────────────────────────────────────────────────────

    /**
     * Scores exactly 5 cards.
     *
     * Groups cards by rank (sorted by group size desc, then rank desc) to detect
     * pairs/trips/quads. Checks flush (all same suit) and straight (5 consecutive
     * ranks, including the wheel A-2-3-4-5).
     */
    private fun evaluateFive(cards: List<com.pokerkotlin.core.model.Card>): HandRank {
        require(cards.size == 5)

        // Groups sorted: larger groups first; within same size, higher rank first
        val groups = cards
            .groupBy { it.rank.value }
            .entries
            .sortedWith(compareByDescending<Map.Entry<Int, List<com.pokerkotlin.core.model.Card>>> { it.value.size }
                .thenByDescending { it.key })

        val groupSizes = groups.map { it.value.size }  // e.g. [4,1], [3,2], [2,2,1]
        val groupRanks = groups.map { it.key }         // rank of each group (primary first)

        val ranksDesc = cards.map { it.rank.value }.sortedDescending()
        val isFlush = cards.map { it.suit }.toSet().size == 1
        val isStraight = isStraight(ranksDesc)
        val isWheel = isWheel(ranksDesc)               // A-2-3-4-5

        val category = when {
            isFlush && isStraight && ranksDesc.first() == 14 && !isWheel -> HandCategory.ROYAL_FLUSH
            isFlush && isStraight                                         -> HandCategory.STRAIGHT_FLUSH
            groupSizes == listOf(4, 1)                                    -> HandCategory.FOUR_OF_A_KIND
            groupSizes == listOf(3, 2)                                    -> HandCategory.FULL_HOUSE
            isFlush                                                       -> HandCategory.FLUSH
            isStraight                                                    -> HandCategory.STRAIGHT
            groupSizes == listOf(3, 1, 1)                                 -> HandCategory.THREE_OF_A_KIND
            groupSizes == listOf(2, 2, 1)                                 -> HandCategory.TWO_PAIR
            groupSizes == listOf(2, 1, 1, 1)                              -> HandCategory.ONE_PAIR
            else                                                          -> HandCategory.HIGH_CARD
        }

        // Build tiebreakers appropriate for the category
        val tiebreakers: List<Int> = when (category) {
            // Straights: only the top card matters (wheel top = 5, not ace)
            HandCategory.STRAIGHT,
            HandCategory.STRAIGHT_FLUSH,
            HandCategory.ROYAL_FLUSH -> listOf(if (isWheel) 5 else ranksDesc.first())

            // Flush / high card: compare all five ranks top-down
            HandCategory.FLUSH,
            HandCategory.HIGH_CARD -> ranksDesc

            // Grouped hands: primary group rank, then secondary, then kickers
            // groupRanks is already ordered [primary, secondary, kicker…]
            else -> groupRanks
        }

        return HandRank(category, tiebreakers)
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    /**
     * Returns true when [ranksDesc] forms 5 consecutive values,
     * including the wheel (A-2-3-4-5).
     */
    private fun isStraight(ranksDesc: List<Int>): Boolean {
        val unique = ranksDesc.distinct()
        if (unique.size != 5) return false                   // duplicates → not a straight
        if (ranksDesc.first() - ranksDesc.last() == 4) return true  // normal straight
        return isWheel(ranksDesc)
    }

    /** A-2-3-4-5 (the "wheel" — lowest possible straight). */
    private fun isWheel(ranksDesc: List<Int>): Boolean =
        ranksDesc.toSet() == setOf(14, 5, 4, 3, 2)

    /**
     * Generates all [k]-element combinations from [list].
     * For 7 cards and k=5 this produces 21 combinations — negligible cost.
     */
    private fun <T> combinations(list: List<T>, k: Int): List<List<T>> {
        if (k == 0) return listOf(emptyList())
        if (list.size < k) return emptyList()
        val head = list.first()
        val tail = list.drop(1)
        val withHead = combinations(tail, k - 1).map { listOf(head) + it }
        val withoutHead = combinations(tail, k)
        return withHead + withoutHead
    }
}
