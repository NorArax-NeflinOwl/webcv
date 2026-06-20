package com.pokerkotlin.server.session

import com.pokerkotlin.core.model.PlayerId
import com.pokerkotlin.core.model.TableId
import org.slf4j.LoggerFactory
import org.springframework.stereotype.Component
import java.util.UUID
import java.util.concurrent.ConcurrentHashMap

/**
 * In-memory registry that maps opaque bearer tokens to [PlayerSession] records.
 *
 * Thread-safe via [ConcurrentHashMap] — tokens are write-once, read-many.
 * In a multi-node deployment this would be backed by Redis, but for the
 * single-node use-case an in-memory map is sufficient.
 */
@Component
class SessionRegistry {

    private val log = LoggerFactory.getLogger(SessionRegistry::class.java)

    private val sessions = ConcurrentHashMap<String, PlayerSession>()

    // ─────────────────────────────────────────────────────────────────────────
    // Create
    // ─────────────────────────────────────────────────────────────────────────

    /**
     * Generates a new random token, stores the session, and returns the token.
     */
    fun create(playerId: PlayerId, tableId: TableId): String {
        val token = UUID.randomUUID().toString()
        sessions[token] = PlayerSession(token = token, playerId = playerId, tableId = tableId)
        log.info("Session created [playerId={} tableId={}]", playerId.value, tableId.value)
        return token
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Retrieve
    // ─────────────────────────────────────────────────────────────────────────

    /** Returns the [PlayerSession] for the given token, or null if not found / expired. */
    fun get(token: String): PlayerSession? = sessions[token]

    /**
     * Validates that the token belongs to the given table.
     *
     * @return the [PlayerSession] or null if the token is invalid or belongs to a different table.
     */
    fun validate(token: String, tableId: TableId): PlayerSession? {
        val session = sessions[token] ?: return null
        return if (session.tableId == tableId) session else null
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Revoke
    // ─────────────────────────────────────────────────────────────────────────

    /** Revokes a session token (e.g. when the player disconnects permanently). */
    fun revoke(token: String) {
        val removed = sessions.remove(token)
        if (removed != null) {
            log.info("Session revoked [playerId={} tableId={}]", removed.playerId.value, removed.tableId.value)
        }
    }
}
