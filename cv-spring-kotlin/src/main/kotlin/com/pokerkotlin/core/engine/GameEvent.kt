package com.pokerkotlin.core.engine

import com.pokerkotlin.core.model.TableId

sealed interface GameEvent {
    data class StateChanged(val tableId: TableId) : GameEvent
}