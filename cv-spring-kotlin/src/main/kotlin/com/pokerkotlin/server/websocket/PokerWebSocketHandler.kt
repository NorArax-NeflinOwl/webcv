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
 * The client connects with a `token` query-parameter issued by
 * `POST /api/pokertable/{id}/join`. The handler validates the token against
 * [SessionRegistry] and rejects the connection with 4001 POLICY_VIOLATION
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
 * The `playerId` is never accepted from the client payload — it is always
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
     * Thread-safe session wrappers — Spring's raw [WebSocketSession] is NOT thread-safe,
     * so concurrent sends from the event-collection coroutine and the main handler thread
     * would cause race conditions without this decorator.
     */
    private val safeSessions = ConcurrentHashMap<String, WebSocketSession>()

    // ─────────────────────────────────────────────────────────────────────────
    // Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    override fun afterConnectionEstablished(session: WebSocketSession) {
        val (playerSession, tableId) = authenticate(session) ?: return

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

        // ── Push initial snapshot ──────────────────────────────────────────
        sendSnapshot(safeSession, playerSession, tableId)

        // ── Subscribe to engine events in a background coroutine ──────────
        val scope = CoroutineScope(Dispatchers.IO + SupervisorJob())
        scopes[session.id] = scope
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

    // ─────────────────────────────────────────────────────────────────────────
    // Incoming messages
    // ─────────────────────────────────────────────────────────────────────────

    override fun handleTextMessage(session: WebSocketSession, message: TextMessage) {
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
            // Invalid action parameters (e.g. raise amount exceeds chip stack) — send error to client,
            // do NOT close the session so the player can retry with a valid amount.
            log.warn("[WS] Invalid action args [playerId={}]: {}", playerSession.playerId.value, ex.message)
            safeSession.sendMessage(TextMessage("""{"error":"${ex.message}"}"""))
        } catch (ex: IllegalStateException) {
            // Action not allowed in current game state (e.g. not player's turn) — same treatment.
            log.warn("[WS] Illegal action [playerId={}]: {}", playerSession.playerId.value, ex.message)
            safeSession.sendMessage(TextMessage("""{"error":"${ex.message}"}"""))
        }
    }

    override fun handleTransportError(session: WebSocketSession, exception: Throwable) {
        log.error("[WS] Transport error [wsId={}]: {}", session.id, exception.message)
        scopes.remove(session.id)?.cancel()
        safeSessions.remove(session.id)
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    /**
     * Validates the session token from the query-string.
     *
     * Returns a [Pair] of (PlayerSession, TableId) on success,
     * or closes the connection and returns null on failure.
     */
    private fun authenticate(session: WebSocketSession): Pair<PlayerSession, TableId>? {
        val path = session.uri?.path ?: ""
        val tableId = extractTableId(path)
            ?: run {
                log.warn("[WS] Cannot extract tableId from path: {}", path)
                session.close(CloseStatus(4001, "Malformed URL"))
                return null
            }

        val token = session.uri?.query
            ?.split("&")
            ?.firstOrNull { it.startsWith("token=") }
            ?.removePrefix("token=")

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
    }
}
