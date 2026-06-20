package com.pokerkotlin.core.model

data class Player(
    val id: PlayerId,
    val name: String,
    val coins: Int,
    val holeCards: Hand,
    val isBot: Boolean = true,
    val active: Boolean = false,
    val folded: Boolean = false,
)
