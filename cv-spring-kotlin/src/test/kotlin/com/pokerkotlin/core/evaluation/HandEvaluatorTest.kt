package com.pokerkotlin.core.evaluation

import com.pokerkotlin.core.model.Card
import com.pokerkotlin.core.model.Hand
import com.pokerkotlin.core.model.Rank
import com.pokerkotlin.core.model.Suit
import org.junit.jupiter.api.Assertions.*
import org.junit.jupiter.api.Test

class HandEvaluatorTest {

    private fun c(rank: Rank, suit: Suit) = Card(rank, suit)
    private fun h(vararg cards: Card)     = Hand(cards.toList())

    // ── All 10 hand categories ───────────────────────────────

    @Test
    fun `high card — no matching pattern`() {
        val hand = h(
            c(Rank.TWO,   Suit.RED_HEART),
            c(Rank.FOUR,  Suit.BLACK_SPADE),
            c(Rank.SIX,   Suit.GREEN_CLUB),
            c(Rank.EIGHT, Suit.BLUE_DIAMOND),
            c(Rank.TEN,   Suit.RED_HEART),
        )
        val rank = HandEvaluator.evaluate(hand)
        assertEquals(HandCategory.HIGH_CARD, rank.category)
        assertEquals(listOf(10, 8, 6, 4, 2), rank.tiebreakers)
    }

    @Test
    fun `one pair`() {
        val hand = h(
            c(Rank.ACE,   Suit.RED_HEART),
            c(Rank.ACE,   Suit.BLACK_SPADE),
            c(Rank.KING,  Suit.GREEN_CLUB),
            c(Rank.QUEEN, Suit.BLUE_DIAMOND),
            c(Rank.JACK,  Suit.RED_HEART),
        )
        val rank = HandEvaluator.evaluate(hand)
        assertEquals(HandCategory.ONE_PAIR, rank.category)
        assertEquals(listOf(14, 13, 12, 11), rank.tiebreakers)
    }

    @Test
    fun `two pair`() {
        val hand = h(
            c(Rank.ACE,   Suit.RED_HEART),
            c(Rank.ACE,   Suit.BLACK_SPADE),
            c(Rank.KING,  Suit.GREEN_CLUB),
            c(Rank.KING,  Suit.BLUE_DIAMOND),
            c(Rank.QUEEN, Suit.RED_HEART),
        )
        val rank = HandEvaluator.evaluate(hand)
        assertEquals(HandCategory.TWO_PAIR, rank.category)
        assertEquals(listOf(14, 13, 12), rank.tiebreakers)
    }

    @Test
    fun `three of a kind`() {
        val hand = h(
            c(Rank.ACE,   Suit.RED_HEART),
            c(Rank.ACE,   Suit.BLACK_SPADE),
            c(Rank.ACE,   Suit.GREEN_CLUB),
            c(Rank.KING,  Suit.BLUE_DIAMOND),
            c(Rank.QUEEN, Suit.RED_HEART),
        )
        val rank = HandEvaluator.evaluate(hand)
        assertEquals(HandCategory.THREE_OF_A_KIND, rank.category)
        assertEquals(14, rank.tiebreakers[0])
    }

    @Test
    fun `straight 10-J-Q-K-A`() {
        val hand = h(
            c(Rank.TEN,   Suit.RED_HEART),
            c(Rank.JACK,  Suit.BLACK_SPADE),
            c(Rank.QUEEN, Suit.GREEN_CLUB),
            c(Rank.KING,  Suit.BLUE_DIAMOND),
            c(Rank.ACE,   Suit.RED_HEART),
        )
        val rank = HandEvaluator.evaluate(hand)
        assertEquals(HandCategory.STRAIGHT, rank.category)
        assertEquals(listOf(14), rank.tiebreakers)
    }

    @Test
    fun `wheel straight A-2-3-4-5 — top card is 5 not ace`() {
        val hand = h(
            c(Rank.ACE,   Suit.RED_HEART),
            c(Rank.TWO,   Suit.BLACK_SPADE),
            c(Rank.THREE, Suit.GREEN_CLUB),
            c(Rank.FOUR,  Suit.BLUE_DIAMOND),
            c(Rank.FIVE,  Suit.RED_HEART),
        )
        val rank = HandEvaluator.evaluate(hand)
        assertEquals(HandCategory.STRAIGHT, rank.category)
        assertEquals(listOf(5), rank.tiebreakers)
    }

    @Test
    fun `flush`() {
        val hand = h(
            c(Rank.TWO,   Suit.RED_HEART),
            c(Rank.FIVE,  Suit.RED_HEART),
            c(Rank.SEVEN, Suit.RED_HEART),
            c(Rank.NINE,  Suit.RED_HEART),
            c(Rank.JACK,  Suit.RED_HEART),
        )
        val rank = HandEvaluator.evaluate(hand)
        assertEquals(HandCategory.FLUSH, rank.category)
        assertEquals(listOf(11, 9, 7, 5, 2), rank.tiebreakers)
    }

    @Test
    fun `full house`() {
        val hand = h(
            c(Rank.ACE,  Suit.RED_HEART),
            c(Rank.ACE,  Suit.BLACK_SPADE),
            c(Rank.ACE,  Suit.GREEN_CLUB),
            c(Rank.KING, Suit.BLUE_DIAMOND),
            c(Rank.KING, Suit.RED_HEART),
        )
        val rank = HandEvaluator.evaluate(hand)
        assertEquals(HandCategory.FULL_HOUSE, rank.category)
        assertEquals(listOf(14, 13), rank.tiebreakers)
    }

    @Test
    fun `four of a kind`() {
        val hand = h(
            c(Rank.ACE,  Suit.RED_HEART),
            c(Rank.ACE,  Suit.BLACK_SPADE),
            c(Rank.ACE,  Suit.GREEN_CLUB),
            c(Rank.ACE,  Suit.BLUE_DIAMOND),
            c(Rank.KING, Suit.RED_HEART),
        )
        val rank = HandEvaluator.evaluate(hand)
        assertEquals(HandCategory.FOUR_OF_A_KIND, rank.category)
        assertEquals(listOf(14, 13), rank.tiebreakers)
    }

    @Test
    fun `straight flush`() {
        val hand = h(
            c(Rank.FIVE,  Suit.RED_HEART),
            c(Rank.SIX,   Suit.RED_HEART),
            c(Rank.SEVEN, Suit.RED_HEART),
            c(Rank.EIGHT, Suit.RED_HEART),
            c(Rank.NINE,  Suit.RED_HEART),
        )
        val rank = HandEvaluator.evaluate(hand)
        assertEquals(HandCategory.STRAIGHT_FLUSH, rank.category)
        assertEquals(listOf(9), rank.tiebreakers)
    }

    @Test
    fun `royal flush`() {
        val hand = h(
            c(Rank.TEN,   Suit.RED_HEART),
            c(Rank.JACK,  Suit.RED_HEART),
            c(Rank.QUEEN, Suit.RED_HEART),
            c(Rank.KING,  Suit.RED_HEART),
            c(Rank.ACE,   Suit.RED_HEART),
        )
        val rank = HandEvaluator.evaluate(hand)
        assertEquals(HandCategory.ROYAL_FLUSH, rank.category)
    }

    // ── 7-card best-5 selection ──────────────────────────────

    @Test
    fun `7 cards — picks the best 5-card combination`() {
        // Royal Flush hidden among 7 cards (+ 2 irrelevant cards)
        val hand = h(
            c(Rank.TEN,   Suit.RED_HEART),
            c(Rank.JACK,  Suit.RED_HEART),
            c(Rank.QUEEN, Suit.RED_HEART),
            c(Rank.KING,  Suit.RED_HEART),
            c(Rank.ACE,   Suit.RED_HEART),
            c(Rank.TWO,   Suit.BLACK_SPADE),
            c(Rank.THREE, Suit.GREEN_CLUB),
        )
        val rank = HandEvaluator.evaluate(hand)
        assertEquals(HandCategory.ROYAL_FLUSH, rank.category)
    }

    @Test
    fun `7 cards — ignores weaker combos, selects full house over pair`() {
        val hand = h(
            c(Rank.KING,  Suit.RED_HEART),
            c(Rank.KING,  Suit.BLACK_SPADE),
            c(Rank.KING,  Suit.GREEN_CLUB),
            c(Rank.QUEEN, Suit.BLUE_DIAMOND),
            c(Rank.QUEEN, Suit.RED_HEART),
            c(Rank.TWO,   Suit.GREEN_CLUB),
            c(Rank.THREE, Suit.BLUE_DIAMOND),
        )
        val rank = HandEvaluator.evaluate(hand)
        assertEquals(HandCategory.FULL_HOUSE, rank.category)
        assertEquals(listOf(13, 12), rank.tiebreakers)
    }

    // ── Tiebreakers ──────────────────────────────────────────

    @Test
    fun `kicker decides between equal pairs of aces`() {
        val highKicker = h(
            c(Rank.ACE,   Suit.RED_HEART),
            c(Rank.ACE,   Suit.BLACK_SPADE),
            c(Rank.KING,  Suit.GREEN_CLUB),
            c(Rank.QUEEN, Suit.BLUE_DIAMOND),
            c(Rank.JACK,  Suit.RED_HEART),
        )
        val lowKicker = h(
            c(Rank.ACE,   Suit.GREEN_CLUB),
            c(Rank.ACE,   Suit.BLUE_DIAMOND),
            c(Rank.NINE,  Suit.RED_HEART),
            c(Rank.EIGHT, Suit.BLACK_SPADE),
            c(Rank.SEVEN, Suit.GREEN_CLUB),
        )
        assertTrue(HandEvaluator.evaluate(highKicker) > HandEvaluator.evaluate(lowKicker))
    }

    @Test
    fun `two flushes — higher kicker wins`() {
        val strongFlush = h(
            c(Rank.ACE,   Suit.GREEN_CLUB),
            c(Rank.KING,  Suit.GREEN_CLUB),
            c(Rank.QUEEN, Suit.GREEN_CLUB),
            c(Rank.JACK,  Suit.GREEN_CLUB),
            c(Rank.NINE,  Suit.GREEN_CLUB),
        )
        val weakFlush = h(
            c(Rank.ACE,   Suit.BLUE_DIAMOND),
            c(Rank.KING,  Suit.BLUE_DIAMOND),
            c(Rank.QUEEN, Suit.BLUE_DIAMOND),
            c(Rank.JACK,  Suit.BLUE_DIAMOND),
            c(Rank.EIGHT, Suit.BLUE_DIAMOND),
        )
        assertTrue(HandEvaluator.evaluate(strongFlush) > HandEvaluator.evaluate(weakFlush))
    }

    @Test
    fun `identical hands compare as equal`() {
        val a = h(
            c(Rank.ACE,  Suit.RED_HEART),
            c(Rank.ACE,  Suit.BLACK_SPADE),
            c(Rank.ACE,  Suit.GREEN_CLUB),
            c(Rank.ACE,  Suit.BLUE_DIAMOND),
            c(Rank.KING, Suit.RED_HEART),
        )
        val b = h(
            c(Rank.ACE,  Suit.RED_HEART),
            c(Rank.ACE,  Suit.BLACK_SPADE),
            c(Rank.ACE,  Suit.GREEN_CLUB),
            c(Rank.ACE,  Suit.BLUE_DIAMOND),
            c(Rank.KING, Suit.RED_HEART),
        )
        assertEquals(0, HandEvaluator.evaluate(a).compareTo(HandEvaluator.evaluate(b)))
    }

    // ── Validation ───────────────────────────────────────────

    @Test
    fun `error when too few cards — 2`() {
        val hand = h(c(Rank.ACE, Suit.RED_HEART), c(Rank.KING, Suit.BLACK_SPADE))
        assertThrows(IllegalArgumentException::class.java) { HandEvaluator.evaluate(hand) }
    }

    @Test
    fun `error when too many cards — 8`() {
        val hand = h(
            c(Rank.ACE,   Suit.RED_HEART),    c(Rank.KING,  Suit.BLACK_SPADE),
            c(Rank.QUEEN, Suit.GREEN_CLUB),   c(Rank.JACK,  Suit.BLUE_DIAMOND),
            c(Rank.TEN,   Suit.RED_HEART),    c(Rank.NINE,  Suit.BLACK_SPADE),
            c(Rank.EIGHT, Suit.GREEN_CLUB),   c(Rank.SEVEN, Suit.BLUE_DIAMOND),
        )
        assertThrows(IllegalArgumentException::class.java) { HandEvaluator.evaluate(hand) }
    }
}
