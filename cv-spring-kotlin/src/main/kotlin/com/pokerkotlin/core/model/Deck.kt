package com.pokerkotlin.core.model

import java.security.SecureRandom

class Deck private constructor(private val cards: MutableList<Card>) {

    val remaining: Int get() = cards.size

    fun draw(): Card {
        check(cards.isNotEmpty()) { "Deck is empty" }
        return cards.removeAt(0)
    }

    fun drawMany(n: Int): List<Card> = List(n) { draw() }

    companion object {
        /** Returns a freshly shuffled full 52-card deck. */
        fun shuffle(random: SecureRandom = SecureRandom()): Deck {
            val all = DeckFactory.freshDeck().toMutableList()
            for (i in all.indices.reversed()) {
                val j = random.nextInt(i + 1)
                all[i] = all[j].also { all[j] = all[i] }
            }
            return Deck(all)
        }

        /**
         * Creates a [Deck] from the given cards in the exact order supplied.
         * Intended for tests — lets callers control exactly which cards are dealt.
         */
        fun of(vararg cards: Card): Deck = Deck(cards.toMutableList())
    }
}
