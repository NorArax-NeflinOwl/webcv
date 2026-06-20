package com.pokerkotlin.core.evaluation

import org.junit.jupiter.api.Assertions.*
import org.junit.jupiter.api.Test

class HandRankTest {

    @Test
    fun `higher category wins regardless of tiebreakers`() {
        val flush    = HandRank(HandCategory.FLUSH,    listOf(2, 3, 4, 5, 7))
        val straight = HandRank(HandCategory.STRAIGHT, listOf(14))
        assertTrue(flush > straight)
    }

    @Test
    fun `same category — higher first tiebreaker wins`() {
        val highPair = HandRank(HandCategory.ONE_PAIR, listOf(14, 13, 12))
        val lowPair  = HandRank(HandCategory.ONE_PAIR, listOf(14, 10, 9))
        assertTrue(highPair > lowPair)
    }

    @Test
    fun `same category — second tiebreaker decides when first is equal`() {
        val a = HandRank(HandCategory.TWO_PAIR, listOf(14, 13, 12))
        val b = HandRank(HandCategory.TWO_PAIR, listOf(14, 13, 2))
        assertTrue(a > b)
    }

    @Test
    fun `identical category and tiebreakers return zero`() {
        val a = HandRank(HandCategory.TWO_PAIR, listOf(14, 13, 12))
        val b = HandRank(HandCategory.TWO_PAIR, listOf(14, 13, 12))
        assertEquals(0, a.compareTo(b))
    }

    @Test
    fun `comparison is antisymmetric`() {
        val strong = HandRank(HandCategory.FOUR_OF_A_KIND, listOf(14, 2))
        val weak   = HandRank(HandCategory.FULL_HOUSE,     listOf(13, 12))
        assertTrue(strong > weak)
        assertTrue(weak < strong)
    }

    @Test
    fun `royal flush beats straight flush`() {
        val royal    = HandRank(HandCategory.ROYAL_FLUSH,    listOf(14))
        val straight = HandRank(HandCategory.STRAIGHT_FLUSH, listOf(13))
        assertTrue(royal > straight)
    }
}
