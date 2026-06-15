package com.pokerkotlin.core.strategy

import com.pokerkotlin.core.engine.PlayerAction
import com.pokerkotlin.core.engine.PlayerView
import com.pokerkotlin.core.engine.TableView

interface BotStrategy {
    fun decide(view: TableView, player: PlayerView): PlayerAction
}