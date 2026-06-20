package com.pokerkotlin.server.controller

import com.pokerkotlin.core.model.Hand
import com.pokerkotlin.core.model.Player
import com.pokerkotlin.core.model.PlayerId
import com.pokerkotlin.core.model.TableId
import com.pokerkotlin.core.strategy.RandomBotStrategy
import com.pokerkotlin.server.dto.CreateTableRequest
import com.pokerkotlin.server.dto.CreateTableResponse
import com.pokerkotlin.server.dto.JoinTableRequest
import com.pokerkotlin.server.dto.JoinTableResponse
import com.pokerkotlin.server.dto.TableInfoDto
import com.pokerkotlin.server.dto.TableListResponse
import com.pokerkotlin.server.dto.TableSnapshotDto
import com.pokerkotlin.server.dto.toDto
import com.pokerkotlin.server.repository.TableRepository
import com.pokerkotlin.server.session.SessionRegistry
import kotlinx.coroutines.runBlocking
import org.springframework.http.HttpStatus
import org.springframework.web.bind.annotation.GetMapping
import org.springframework.web.bind.annotation.PathVariable
import org.springframework.web.bind.annotation.PostMapping
import org.springframework.web.bind.annotation.RequestBody
import org.springframework.web.bind.annotation.RequestMapping
import org.springframework.web.bind.annotation.RequestParam
import org.springframework.web.bind.annotation.ResponseStatus
import org.springframework.web.bind.annotation.RestController
import org.springframework.web.server.ResponseStatusException
import java.security.SecureRandom
import java.util.UUID

/**
 * REST API for managing poker tables.
 *
 * Flow:
 *  1. Client calls POST /tables            → gets tableId
 *  2. Client calls POST /tables/{id}/join  → gets sessionToken + playerId
 *  3. Client calls POST /tables/{id}/start → starts the first hand
 *  4. Client connects to WS /tables/{id}/ws?token=…  (see PokerWebSocketHandler)
 *
 * The `sessionToken` is a bearer credential that authorises the WebSocket connection
 * and all subsequent actions. It is intentionally kept separate from HTTP session
 * cookies so that the same token works for both the REST and WS layers.
 */
@RestController
@RequestMapping("/tables")
class TableController(
    private val tableRepository: TableRepository,
    private val sessionRegistry: SessionRegistry,
) {

    private val secureRandom = SecureRandom()

    // ─────────────────────────────────────────────────────────────────────────
    // GET /tables — list all active tables
    // ─────────────────────────────────────────────────────────────────────────

    /**
     * Returns a list of all currently active table IDs.
     *
     * Example response:
     * ```json
     * { "tables": [{"tableId": "…"}, {"tableId": "…"}], "count": 2 }
     * ```
     */
    @GetMapping
    fun listTables(): TableListResponse {
        val ids = tableRepository.allTableIds()
        return TableListResponse(
            tables = ids.map { TableInfoDto(it.value) },
            count  = ids.size,
        )
    }

    // ─────────────────────────────────────────────────────────────────────────
    // POST /tables — create a new table (with 2 bots pre-seated)
    // ─────────────────────────────────────────────────────────────────────────

    /**
     * Creates a new poker table and pre-seats two RandomBot players.
     *
     * Body (optional JSON): `{"blindAmount": 10}`
     *
     * Returns: `{"tableId": "<uuid>"}`
     */
    @PostMapping
    @ResponseStatus(HttpStatus.CREATED)
    fun createTable(@RequestBody(required = false) body: CreateTableRequest?): CreateTableResponse {
        val blind = body?.blindAmount ?: 10
        val tableId = tableRepository.create(blindAmount = blind)
        val engine = tableRepository.getOrThrow(tableId)

        // Pre-seat two bots so the human only needs to join and start
        repeat(2) { i ->
            engine.addPlayer(
                player = Player(
                    id = PlayerId(UUID.randomUUID().toString()),
                    name = "Bot ${i + 1}",
                    coins = 1_000,
                    holeCards = Hand(emptyList()),
                    isBot = true,
                ),
                strategy = RandomBotStrategy(secureRandom),
            )
        }

        return CreateTableResponse(tableId.value)
    }

    // ─────────────────────────────────────────────────────────────────────────
    // POST /tables/{id}/join — add a human player and issue a session token
    // ─────────────────────────────────────────────────────────────────────────

    /**
     * Adds a human player to an existing table and returns a session token.
     *
     * Body: `{"playerName": "Alice", "coins": 1000}`
     *
     * Returns: `{"sessionToken": "<uuid>", "playerId": "<uuid>"}`
     *
     * The token must be passed as `?token=…` on the WebSocket URL.
     */
    @PostMapping("/{id}/join")
    @ResponseStatus(HttpStatus.CREATED)
    fun joinTable(
        @PathVariable id: String,
        @RequestBody body: JoinTableRequest,
    ): JoinTableResponse {
        val tableId = TableId(id)
        val engine = tableRepository.get(tableId)
            ?: throw ResponseStatusException(HttpStatus.NOT_FOUND, "Table not found: $id")

        val playerId = PlayerId(UUID.randomUUID().toString())
        engine.addPlayer(
            player = Player(
                id = playerId,
                name = body.playerName,
                coins = body.coins,
                holeCards = Hand(emptyList()),
                isBot = false,
            ),
            strategy = null,    // human — actions come through WebSocket
        )

        val token = sessionRegistry.create(playerId = playerId, tableId = tableId)
        return JoinTableResponse(sessionToken = token, playerId = playerId.value)
    }

    // ─────────────────────────────────────────────────────────────────────────
    // POST /tables/{id}/start — start the first (or next) hand
    // ─────────────────────────────────────────────────────────────────────────

    /**
     * Starts a new hand on the table.
     *
     * Requires the session token in the `X-Session-Token` header.
     * Returns 204 No Content on success.
     */
    @PostMapping("/{id}/start")
    @ResponseStatus(HttpStatus.NO_CONTENT)
    fun startHand(
        @PathVariable id: String,
        @RequestParam token: String,
    ) {
        val tableId = TableId(id)
        sessionRegistry.validate(token, tableId)
            ?: throw ResponseStatusException(HttpStatus.UNAUTHORIZED, "Invalid or expired session token")

        val engine = tableRepository.get(tableId)
            ?: throw ResponseStatusException(HttpStatus.NOT_FOUND, "Table not found: $id")

        // startHand is a suspend function — bridge to blocking context
        runBlocking { engine.startHand() }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GET /tables/{id}/snapshot — REST snapshot (polling fallback)
    // ─────────────────────────────────────────────────────────────────────────

    /**
     * Returns a JSON snapshot of the table from the requesting player's perspective.
     *
     * Use this as a fallback when WebSocket is not available.
     * Normal clients should rely on the WS push instead.
     */
    @GetMapping("/{id}/snapshot")
    fun snapshot(
        @PathVariable id: String,
        @RequestParam token: String,
    ): TableSnapshotDto {
        val tableId = TableId(id)
        val session = sessionRegistry.validate(token, tableId)
            ?: throw ResponseStatusException(HttpStatus.UNAUTHORIZED, "Invalid or expired session token")

        val engine = tableRepository.get(tableId)
            ?: throw ResponseStatusException(HttpStatus.NOT_FOUND, "Table not found: $id")

        return engine.snapshot(forPlayer = session.playerId).toDto()
    }
}
