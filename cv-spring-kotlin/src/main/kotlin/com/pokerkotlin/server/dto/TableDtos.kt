package com.pokerkotlin.server.dto

// ── REST: GET /api/pokertable ─────────────────────────────────────────────────

data class TableListResponse(val tables: List<TableInfoDto>, val count: Int)

data class TableInfoDto(val tableId: String)

// ── REST: POST /api/pokertable ────────────────────────────────────────────────

data class CreateTableRequest(val blindAmount: Int = 10)

data class CreateTableResponse(val tableId: String)

// ── REST: POST /api/pokertable/{id}/join ──────────────────────────────────────

data class JoinTableRequest(
    val playerName: String,
    val coins: Int = 1_000,
)

data class JoinTableResponse(
    val sessionToken: String,
    val playerId: String,
)
