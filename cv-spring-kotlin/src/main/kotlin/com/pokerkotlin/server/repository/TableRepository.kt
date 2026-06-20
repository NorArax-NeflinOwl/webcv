package com.pokerkotlin.server.repository

import com.pokerkotlin.core.engine.GameEngine
import com.pokerkotlin.core.model.TableId
import org.slf4j.LoggerFactory
import org.springframework.stereotype.Repository
import java.util.UUID
import java.util.concurrent.ConcurrentHashMap

/**
 * In-memory repository of active poker tables.
 *
 * Each table is a [GameEngine] instance identified by a [TableId].
 * Uses [ConcurrentHashMap] — safe under concurrent HTTP/WebSocket requests
 * without a global lock (each [GameEngine] has its own per-table Mutex).
 */
@Repository
class TableRepository {

    private val log = LoggerFactory.getLogger(TableRepository::class.java)

    private val tables = ConcurrentHashMap<String, GameEngine>()

    // ─────────────────────────────────────────────────────────────────────────
    // Create / retrieve
    // ─────────────────────────────────────────────────────────────────────────

    /**
     * Creates a new table with a random UUID and returns its [TableId].
     */
    fun create(blindAmount: Int = 10): TableId {
        val id = TableId(UUID.randomUUID().toString())
        val engine = GameEngine(tableId = id, blindAmount = blindAmount)
        tables[id.value] = engine
        if (log.isInfoEnabled) log.info("Table created [tableId={}] (blind={})", id.value, blindAmount)
        return id
    }

    /**
     * Returns the [GameEngine] for the given table, or null if not found.
     */
    fun get(tableId: TableId): GameEngine? {
        val engine = tables[tableId.value]
        if (engine == null && log.isWarnEnabled) {
            log.warn("Table not found [tableId={}]", tableId.value)
        }
        return engine
    }

    /**
     * Returns the [GameEngine] or throws [NoSuchElementException] — convenient in routing.
     */
    fun getOrThrow(tableId: TableId): GameEngine =
        get(tableId) ?: throw NoSuchElementException("Table not found: ${tableId.value}")

    // ─────────────────────────────────────────────────────────────────────────
    // Remove
    // ─────────────────────────────────────────────────────────────────────────

    /**
     * Removes a table (e.g. when all players have left).
     * Returns true if the table existed and was removed.
     */
    fun remove(tableId: TableId): Boolean {
        val removed = tables.remove(tableId.value) != null
        if (removed && log.isInfoEnabled) {
            log.info("Table removed [tableId={}]", tableId.value)
        } else if (!removed && log.isWarnEnabled) {
            log.warn("Attempted to remove non-existent table [tableId={}]", tableId.value)
        }
        return removed
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Utilities
    // ─────────────────────────────────────────────────────────────────────────

    /** Number of currently active tables — useful for metrics / actuator. */
    fun count(): Int = tables.size

    /** Snapshot of all active table IDs (order not guaranteed). */
    fun allTableIds(): List<TableId> = tables.keys.map { TableId(it) }
}
