package com.pokerkotlin.core.engine

import com.pokerkotlin.core.model.Card
import com.pokerkotlin.core.model.PlayerId

data class PlayerView(
    val id: PlayerId,
    val name: String,
    val coins: Int,
    val folded: Boolean,
    val betThisRound: Int,
    val holeCards: List<Card>?
)
