package com.pokerkotlin.core.engine

import com.pokerkotlin.core.model.Card
import com.pokerkotlin.core.model.PlayerId

data class TableView(
    val state: GameState,
    val board: List<Card>,
    val pot: Int,
    val toCall: Int,
    val players: List<PlayerView>,
    val currentTurnId: PlayerId?
)
