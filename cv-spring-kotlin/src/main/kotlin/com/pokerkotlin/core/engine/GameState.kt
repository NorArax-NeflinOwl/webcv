package com.pokerkotlin.core.engine

import com.pokerkotlin.core.evaluation.PlayerResult
import com.pokerkotlin.core.model.Card

sealed interface GameState {
    data object WaitingForPlayers : GameState
    data class PreFlop(val pot: Int) : GameState
    data class Flop(val pot: Int, val board: List<Card>) : GameState       // 3 cards
    data class Turn(val pot: Int, val board: List<Card>) : GameState       // 4 cards
    data class River(val pot: Int, val board: List<Card>) : GameState      // 5 cards
    data class Showdown(val pot: Int, val board: List<Card>, val ranking: List<PlayerResult>) : GameState
}