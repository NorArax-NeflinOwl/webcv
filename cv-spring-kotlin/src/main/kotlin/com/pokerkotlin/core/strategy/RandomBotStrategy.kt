package com.pokerkotlin.core.strategy

import com.pokerkotlin.core.engine.PlayerAction
import com.pokerkotlin.core.engine.PlayerView
import com.pokerkotlin.core.engine.TableView
import java.security.SecureRandom

class RandomBotStrategy(private val random: SecureRandom) : BotStrategy {
    override fun decide(view: TableView, player: PlayerView): PlayerAction {
        val canCheck = view.toCall == 0
        return when {
            random.nextInt(10) == 0 -> PlayerAction.Fold(player.id)
            canCheck -> PlayerAction.Check(player.id)
            else -> PlayerAction.Call(player.id)
        }
    }
}