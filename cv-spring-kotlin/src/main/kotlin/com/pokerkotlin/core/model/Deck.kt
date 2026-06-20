package com.pokerkotlin.core.model

import java.security.SecureRandom

class Deck private constructor(private val cards: MutableList<Card>) {

    val remaining: Int get() = cards.size

    fun draw(): Card {
        if ( cards.isEmpty() ) { "Deck is empty" }
        return cards.removeAt(0)
    }

    fun drawMany(n: Int): List<Card> = List(n) { draw() }

    companion object {
        fun shuffle(random: SecureRandom = SecureRandom()): Deck {
            val all = DeckFactory.freshDeck().toMutableList()
            for (i in all.indices.reversed()) {
                val j = random.nextInt(i + 1)
                all[i] = all[j].also { all[j] = all[i] }
            }
            return Deck(all)
        }

        /** Creates a [Deck] from a specific ordered list of cards — intended for tests only. */
        fun of(vararg cards: Card): Deck = Deck(cards.toMutableList())
    }
}
