package com.pokerkotlin.server.websocket

import com.fasterxml.jackson.databind.ObjectMapper
import com.pokerkotlin.core.engine.GameEvent
import com.pokerkotlin.core.model.TableId
import com.pokerkotlin.server.dto.ActionRequest
import com.pokerkotlin.server.dto.TableSnapshotDto
import com.pokerkotlin.server.dto.toDomain
import com.pokerkotlin.server.dto.toDto
import com.pokerkotlin.server.repository.TableRepository
import com.pokerkotlin.server.session.PlayerSession
import com.pokerkotlin.server.session.SessionRegistry
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch
import kotlinx.coroutines.runBlocking
import org.slf4j.LoggerFactory
import org.springframework.web.socket.CloseStatus
import org.springframework.web.socket.TextMessage
import org.springframework.web.socket.WebSocketSession
import org.springframework.web.socket.handler.ConcurrentWebSocketSessionDecorator
import org.springframework.web.socket.handler.TextWebSocketHandler
import java.util.concurrent.ConcurrentHashMap

/**
 * WebSocket handler for `/api/pokertable/{id}/ws?token=<sessionToken>`.
 *
 * ## Protocol
 *
 * ### Handshake / authorisation
 * The client sends the token issued by `POST /api/pokertable/{id}/join` as its first
 * message, `{"type":"Auth","token":"…"}`, within 10 seconds, so that the token stays
 * out of the URL (and out of the gateway's access log). Older clients may still pass
 * it as a `token` query-parameter. The handler validates the token against
 * [SessionRegistry] and closes the connection with 4001 POLICY_VIOLATION
 * if it is invalid or belongs to a different table.
 *
 * ### Server → Client (push)
 * After each state change the handler sends a [TableSnapshotDto] JSON object
 * that reflects the requesting player's perspective (opponent hole cards
 * are omitted until Showdown).
 *
 * ### Client → Server (actions)
 * The client sends a JSON action object with a `"type"` discriminator:
 * ```json
 * {"type":"Fold"}
 * {"type":"Check"}
 * {"type":"Call"}
 * {"type":"Raise","amount":50}
 * ```
 * The `playerId` is never accepted from the client payload  it is always
 * resolved from the server-side session to prevent impersonation.
 */
class PokerWebSocketHandler(
    private val tableRepository: TableRepository,
    private val sessionRegistry: SessionRegistry,
    private val objectMapper: ObjectMapper,
) : TextWebSocketHandler() {

    private val log = LoggerFactory.getLogger(javaClass)

    /** Per-connection coroutine scope used to collect engine events. */
    private val scopes = ConcurrentHashMap<String, CoroutineScope>()

    /**
     * Thread-safe session wrappers  Spring's raw [WebSocketSession] is NOT thread-safe,
     * so concurrent sends from the event-collection coroutine and the main handler thread
     * would cause race conditions without this decorator.
     */
    private val safeSessions = ConcurrentHashMap<String, WebSocketSession>()

    // 
    // Lifecycle
    // 

    override fun afterConnectionEstablished(session: WebSocketSession) {
        if (queryToken(session) == null) {
            // The page's way: wait for {"type":"Auth","token":"…"} (see handleTextMessage).
            session.attributes[ATTR_AWAITING_AUTH] = true
            val scope = CoroutineScope(Dispatchers.IO + SupervisorJob())
            scopes[session.id] = scope
            scope.launch {
                delay(AUTH_TIMEOUT_MS)
                if (session.attributes[ATTR_AWAITING_AUTH] == true && session.isOpen) {
                    log.warn("[WS] No authentication within {} ms [wsId={}]", AUTH_TIMEOUT_MS, session.id)
                    session.close(CloseStatus(4001, "Authentication timeout"))
                }
            }
            return
        }
        val (playerSession, tableId) = authenticate(session, queryToken(session)) ?: return
        onAuthenticated(session, playerSession, tableId)
    }

    private fun onAuthenticated(session: WebSocketSession, playerSession: PlayerSession, tableId: TableId) {
        val engine = tableRepository.get(tableId)
            ?: run {
                log.warn("[WS] Table not found [tableId={}]", tableId.value)
                session.close(CloseStatus(4004, "Table not found"))
                return
            }

        // Wrap in a thread-safe decorator so the coroutine and Spring handler thread
        // can both call sendMessage concurrently without race conditions.
        // Limits: 5 s send timeout, 64 KB output buffer.
        val safeSession = ConcurrentWebSocketSessionDecorator(session, 5_000, 64 * 1_024)
        safeSessions[session.id] = safeSession

        // Stash resolved session data for use in handleTextMessage
        session.attributes[ATTR_PLAYER_SESSION] = playerSession
        log.info("[WS] Connected [playerId={} tableId={} wsId={}]",
            playerSession.playerId.value, tableId.value, session.id)

        //  Push initial snapshot 
        sendSnapshot(safeSession, playerSession, tableId)

        //  Subscribe to engine events in a background coroutine 
        val scope = CoroutineScope(Dispatchers.IO + SupervisorJob())
        scopes.put(session.id, scope)?.cancel()  // also ends the authentication timeout
        scope.launch {
            engine.events.collect { event ->
                when (event) {
                    is GameEvent.StateChanged -> {
                        if (safeSession.isOpen) {
                            sendSnapshot(safeSession, playerSession, tableId)
                        }
                    }
                }
            }
        }
    }

    override fun afterConnectionClosed(session: WebSocketSession, status: CloseStatus) {
        scopes.remove(session.id)?.cancel()
        safeSessions.remove(session.id)
        val ps = session.attributes[ATTR_PLAYER_SESSION] as? PlayerSession
        if (ps != null) {
            log.info("[WS] Disconnected [playerId={} tableId={} status={}]",
                ps.playerId.value, ps.tableId.value, status)
        }
    }

    // 
    // Incoming messages
    // 

    override fun handleTextMessage(session: WebSocketSession, message: TextMessage) {
        if (session.attributes.remove(ATTR_AWAITING_AUTH) == true) {
            val token = try {
                val auth = objectMapper.readTree(message.payload)
                if (auth.path("type").asText() == "Auth") auth.path("token").asText().ifEmpty { null } else null
            } catch (ex: Exception) {
                null
            }
            val (playerSession, tableId) = authenticate(session, token) ?: return
            onAuthenticated(session, playerSession, tableId)
            return
        }

        val safeSession = safeSessions[session.id] ?: session
        val playerSession = session.attributes[ATTR_PLAYER_SESSION] as? PlayerSession
            ?: run {
                session.close(CloseStatus(4001, "Unauthenticated"))
                return
            }

        val engine = tableRepository.get(playerSession.tableId)
            ?: run {
                session.close(CloseStatus(4004, "Table not found"))
                return
            }

        val actionRequest = try {
            objectMapper.readValue(message.payload, ActionRequest::class.java)
        } catch (ex: Exception) {
            log.warn("[WS] Bad action payload [wsId={} payload={}]: {}",
                session.id, message.payload, ex.message)
            safeSession.sendMessage(TextMessage("""{"error":"Invalid action format"}"""))
            return
        }

        val domainAction = actionRequest.toDomain(playerSession.playerId)

        try {
            runBlocking { engine.applyAction(domainAction) }
        } catch (ex: IllegalArgumentException) {
            // Invalid action parameters (e.g. raise amount exceeds chip stack)  send error to client,
            // do NOT close the session so the player can retry with a valid amount.
            log.warn("[WS] Invalid action args [playerId={}]: {}", playerSession.playerId.value, ex.message)
            safeSession.sendMessage(TextMessage("""{"error":"${ex.message}"}"""))
        } catch (ex: IllegalStateException) {
            // Action not allowed in current game state (e.g. not player's turn)  same treatment.
            log.warn("[WS] Illegal action [playerId={}]: {}", playerSession.playerId.value, ex.message)
            safeSession.sendMessage(TextMessage("""{"error":"${ex.message}"}"""))
        }
    }

    override fun handleTransportError(session: WebSocketSession, exception: Throwable) {
        log.error("[WS] Transport error [wsId={}]: {}", session.id, exception.message)
        scopes.remove(session.id)?.cancel()
        safeSessions.remove(session.id)
    }

    // 
    // Helpers
    // 

    /** The older clients' `?token=…`, or null. */
    private fun queryToken(session: WebSocketSession): String? = session.uri?.query
        ?.split("&")
        ?.firstOrNull { it.startsWith("token=") }
        ?.removePrefix("token=")
        ?.ifBlank { null }

    /**
     * Validates the session token (from the first message or the query-string).
     *
     * Returns a [Pair] of (PlayerSession, TableId) on success,
     * or closes the connection and returns null on failure.
     */
    private fun authenticate(session: WebSocketSession, token: String?): Pair<PlayerSession, TableId>? {
        val path = session.uri?.path ?: ""
        val tableId = extractTableId(path)
            ?: run {
                log.warn("[WS] Cannot extract tableId from path: {}", path)
                session.close(CloseStatus(4001, "Malformed URL"))
                return null
            }

        if (token.isNullOrBlank()) {
            log.warn("[WS] Missing token [tableId={}]", tableId.value)
            session.close(CloseStatus(4001, "Missing session token"))
            return null
        }

        val playerSession = sessionRegistry.validate(token, tableId)
            ?: run {
                log.warn("[WS] Invalid token [tableId={}]", tableId.value)
                session.close(CloseStatus(4001, "Invalid or expired session token"))
                return null
            }

        return playerSession to tableId
    }

    /** Extracts the `{id}` segment from a path like `/api/pokertable/{id}/ws`. */
    private fun extractTableId(path: String): TableId? {
        // path == "/api/pokertable/<id>/ws"
        val parts = path.trimEnd('/').split("/")
        // parts: ["", "tables", "<id>", "ws"]
        val id = parts.getOrNull(parts.size - 2) ?: return null
        return if (id.isNotBlank()) TableId(id) else null
    }

    private fun sendSnapshot(session: WebSocketSession, playerSession: PlayerSession, tableId: TableId) {
        val engine = tableRepository.get(tableId) ?: return
        val dto: TableSnapshotDto = engine.snapshot(forPlayer = playerSession.playerId).toDto()
        try {
            session.sendMessage(TextMessage(objectMapper.writeValueAsString(dto)))
        } catch (ex: Exception) {
            log.warn("[WS] Failed to send snapshot [wsId={}]: {}", session.id, ex.message)
        }
    }

    private companion object {
        const val ATTR_PLAYER_SESSION = "playerSession"
        const val ATTR_AWAITING_AUTH = "awaitingAuth"
        const val AUTH_TIMEOUT_MS = 10_000L
    }
}
