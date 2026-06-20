package com.pokerkotlin.server.websocket

import com.fasterxml.jackson.databind.ObjectMapper
import com.pokerkotlin.server.repository.TableRepository
import com.pokerkotlin.server.session.SessionRegistry
import org.springframework.context.annotation.Configuration
import org.springframework.web.socket.config.annotation.EnableWebSocket
import org.springframework.web.socket.config.annotation.WebSocketConfigurer
import org.springframework.web.socket.config.annotation.WebSocketHandlerRegistry

/**
 * Registers the [PokerWebSocketHandler] at `/tables/*/ws`.
 *
 * The wildcard `*` captures the table ID, which the handler extracts from
 * `session.uri.path` — Spring's raw WebSocket support does not bind
 * path-variables automatically (unlike STOMP endpoints).
 *
 * `setAllowedOrigins("*")` is intentionally permissive for local development.
 * In production, restrict this to the front-end origin (e.g. `https://your.domain`).
 */
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
