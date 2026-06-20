package com.pokerkotlin.server.dto

// ── REST: POST /tables ────────────────────────────────────────────────────────

data class CreateTableRequest(val blindAmount: Int = 10)

data class CreateTableResponse(val tableId: String)

// ── REST: POST /tables/{id}/join ──────────────────────────────────────────────

data class JoinTableRequest(
    val playerName: String,
    val coins: Int = 1_000,
)

data class JoinTableResponse(
    val sessionToken: String,
    val playerId: String,
)
