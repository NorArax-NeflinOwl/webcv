package com.pokerkotlin.core.engine

import com.pokerkotlin.core.model.PlayerId

sealed interface PlayerAction {
    val playerId: PlayerId
    data class Fold(override val playerId: PlayerId) : PlayerAction
    data class Check(override val playerId: PlayerId) : PlayerAction
    data class Call(override val playerId: PlayerId) : PlayerAction
    data class Raise(override val playerId: PlayerId, val amount: Int) : PlayerAction
}