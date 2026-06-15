package com.pokerkotlin.core.model

object DeckFactory {
    fun freshDeck(): List<Card> = Suit.entries.flatMap { suit -> Rank.entries.map { rank -> Card(rank, suit) } }
}