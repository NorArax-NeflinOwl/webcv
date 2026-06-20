package com.pokerkotlin.server.websocket

import com.fasterxml.jackson.databind.ObjectMapper
import com.pokerkotlin.server.repository.TableRepository
import com.pokerkotlin.server.session.SessionRegistry
import org.springframework.context.annotation.Configuration
import org.springframework.web.socket.config.annotation.EnableWebSocket
import org.springframework.web.socket.config.annotation.WebSocketConfigurer
import org.springframework.web.socket.config.annotation.WebSocketHandlerRegistry

@Configuration
@EnableWebSocket
class WebSocketConfig(
    private val tableRepository: TableRepository,
    private val sessionRegistry: SessionRegistry,
    private val objectMapper: ObjectMapper,
) : WebSocketConfigurer {

    override fun registerWebSocketHandlers(registry: WebSocketHandlerRegistry) {
        registry
            .addHandler(pokerWebSocketHandler(), "/tables/*/ws")
            .setAllowedOrigins("*")
    }

    private fun pokerWebSocketHandler() =
        PokerWebSocketHandler(tableRepository, sessionRegistry, objectMapper)
}
