package com.pokerkotlin.core.model

data class Card(val rank: Rank, val suit: Suit) : Comparable<Card>{
    override fun compareTo(other: Card): Int {
        return compareValuesBy(this, other,
            { it.rank.value },
            { it.suit.symbol }
        )
    }

}
