package com.pokerkotlin.server.session

import com.pokerkotlin.core.model.PlayerId
import com.pokerkotlin.core.model.TableId

/**
 * Immutable record that ties a session token to a specific player at a specific table.
 *
 * @param token  Random UUID string used as the bearer credential (query-param on WS URL).
 * @param playerId  The authenticated player's identity.
 * @param tableId   The table this session belongs to.
 */
data class PlayerSession(
    val token: String,
    val playerId: PlayerId,
    val tableId: TableId,
)
