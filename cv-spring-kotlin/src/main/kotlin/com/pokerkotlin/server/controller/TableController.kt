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
import com.pokerkotlin.server.dto.PokerInput
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
import org.springframework.web.bind.annotation.RequestHeader
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
 *  1. Client calls POST /api/pokertable            → gets tableId
 *  2. Client calls POST /api/pokertable/{id}/join  → gets sessionToken + playerId
 *  3. Client calls POST /api/pokertable/{id}/start → starts the first hand
 *  4. Client connects to WS /api/pokertable/{id}/ws?token=…  (see PokerWebSocketHandler)
 *
 * The `sessionToken` is a bearer credential that authorises the WebSocket connection
 * and all subsequent actions. It is intentionally kept separate from HTTP session
 * cookies so that the same token works for both the REST and WS layers.
 *
 * There is no list of tables: a table's id is known only to whoever created it, and its one human seat
 * is taken by the creator's join (the page plays one human against the bots).
 */
@RestController
@RequestMapping("/api/pokertable")
class TableController(
    private val tableRepository: TableRepository,
    private val sessionRegistry: SessionRegistry,
) {

    private val secureRandom = SecureRandom()


    // 
    // POST /api/pokertable  create a new table (with 2 bots pre-seated)
    // 

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
        PokerInput.checkBlind(blind)?.let { throw ResponseStatusException(HttpStatus.BAD_REQUEST, it) }
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

    // 
    // POST /api/pokertable/{id}/join  add a human player and issue a session token
    // 

    /**
     * Adds a human player to an existing table and returns a session token.
     *
     * Body: `{"playerName": "Alice", "coins": 1000}`
     *
     * Returns: `{"sessionToken": "<uuid>", "playerId": "<uuid>"}`
     *
     * The token must be passed as `?token=…` on the WebSocket URL.
     * 409 when the table's human seat is already taken.
     */
    @PostMapping("/{id}/join")
    @ResponseStatus(HttpStatus.CREATED)
    fun joinTable(
        @PathVariable id: String,
        @RequestBody body: JoinTableRequest,
    ): JoinTableResponse {
        val playerName = PokerInput.checkName(body.playerName)
            .getOrElse { throw ResponseStatusException(HttpStatus.BAD_REQUEST, it.message) }
        PokerInput.checkCoins(body.coins)?.let { throw ResponseStatusException(HttpStatus.BAD_REQUEST, it) }

        val tableId = TableId(id)
        val engine = tableRepository.get(tableId)
            ?: throw ResponseStatusException(HttpStatus.NOT_FOUND, "Table not found: $id")

        val playerId = PlayerId(UUID.randomUUID().toString())
        try {
            engine.addPlayer(
                player = Player(
                    id = playerId,
                    name = playerName,
                    coins = body.coins,
                    holeCards = Hand(emptyList()),
                    isBot = false,
                ),
                strategy = null,    // human  actions come through WebSocket
            )
        } catch (ex: IllegalStateException) {
            throw ResponseStatusException(HttpStatus.CONFLICT, ex.message)
        }

        val token = sessionRegistry.create(playerId = playerId, tableId = tableId)
        return JoinTableResponse(sessionToken = token, playerId = playerId.value)
    }

    // 
    // POST /api/pokertable/{id}/start  start the first (or next) hand
    // 

    /**
     * Starts a new hand on the table.
     *
     * Requires the session token as `Authorization: Bearer <token>` (or the older `?token=…`).
     * Returns 204 No Content on success.
     */
    @PostMapping("/{id}/start")
    @ResponseStatus(HttpStatus.NO_CONTENT)
    fun startHand(
        @PathVariable id: String,
        @RequestHeader(name = "Authorization", required = false) authorization: String?,
        @RequestParam(required = false) token: String?,
    ) {
        val tableId = TableId(id)
        sessionToken(authorization, token)?.let { sessionRegistry.validate(it, tableId) }
            ?: throw ResponseStatusException(HttpStatus.UNAUTHORIZED, "Invalid or expired session token")

        val engine = tableRepository.get(tableId)
            ?: throw ResponseStatusException(HttpStatus.NOT_FOUND, "Table not found: $id")

        // startHand is a suspend function  bridge to blocking context.
        // IllegalArgumentException means the engine rejected the request (e.g. hand already in progress
        // or not enough players)  map to 409 Conflict rather than letting Spring emit a 500.
        try {
            runBlocking { engine.startHand() }
        } catch (ex: IllegalArgumentException) {
            throw ResponseStatusException(HttpStatus.CONFLICT, ex.message)
        }
    }

    // 
    // GET /api/pokertable/{id}/snapshot  REST snapshot (polling fallback)
    // 

    /**
     * Returns a JSON snapshot of the table from the requesting player's perspective.
     *
     * Use this as a fallback when WebSocket is not available.
     * Normal clients should rely on the WS push instead.
     */
    @GetMapping("/{id}/snapshot")
    fun snapshot(
        @PathVariable id: String,
        @RequestHeader(name = "Authorization", required = false) authorization: String?,
        @RequestParam(required = false) token: String?,
    ): TableSnapshotDto {
        val tableId = TableId(id)
        val session = sessionToken(authorization, token)?.let { sessionRegistry.validate(it, tableId) }
            ?: throw ResponseStatusException(HttpStatus.UNAUTHORIZED, "Invalid or expired session token")

        val engine = tableRepository.get(tableId)
            ?: throw ResponseStatusException(HttpStatus.NOT_FOUND, "Table not found: $id")

        return engine.snapshot(forPlayer = session.playerId).toDto()
    }

    /**
     * `Authorization: Bearer <token>` (the page's way: URLs end up in the gateway's access log),
     * else the older `?token=…`.
     */
    private fun sessionToken(authorization: String?, queryToken: String?): String? {
        if (authorization != null && authorization.startsWith("Bearer ", ignoreCase = true)) {
            return authorization.substring("Bearer ".length).trim().ifEmpty { null }
        }
        return queryToken?.ifEmpty { null }
    }
}
