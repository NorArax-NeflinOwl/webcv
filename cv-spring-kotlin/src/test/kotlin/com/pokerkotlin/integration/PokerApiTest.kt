package com.pokerkotlin.integration

import com.cvspringkotlin.CvSpringKotlinApplication
import com.fasterxml.jackson.databind.JsonNode
import com.fasterxml.jackson.databind.ObjectMapper
import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertNotNull
import org.junit.jupiter.api.Test
import org.springframework.beans.factory.annotation.Autowired
import org.springframework.boot.test.context.SpringBootTest
import org.springframework.boot.test.web.client.TestRestTemplate
import org.springframework.boot.test.web.server.LocalServerPort
import org.springframework.http.HttpEntity
import org.springframework.http.HttpHeaders
import org.springframework.http.HttpMethod
import org.springframework.http.HttpStatus
import org.springframework.test.context.ActiveProfiles
import org.springframework.web.socket.CloseStatus
import org.springframework.web.socket.TextMessage
import org.springframework.web.socket.WebSocketSession
import org.springframework.web.socket.client.standard.StandardWebSocketClient
import org.springframework.web.socket.handler.TextWebSocketHandler
import java.util.concurrent.CompletableFuture
import java.util.concurrent.LinkedBlockingQueue
import java.util.concurrent.TimeUnit

/** The protocol the shared pokerGame.js speaks, over a real port: REST with a bearer token, WebSocket with an Auth message. */
@SpringBootTest(classes = [CvSpringKotlinApplication::class], webEnvironment = SpringBootTest.WebEnvironment.RANDOM_PORT)
@ActiveProfiles("test")
class PokerApiTest {

    @LocalServerPort
    private var port: Int = 0

    @Autowired
    private lateinit var rest: TestRestTemplate

    @Autowired
    private lateinit var objectMapper: ObjectMapper

    private fun post(path: String, body: Any?, token: String? = null): Pair<HttpStatus, JsonNode?> {
        val headers = HttpHeaders().apply {
            set(HttpHeaders.CONTENT_TYPE, "application/json")
            if (token != null) setBearerAuth(token)
        }
        val response = rest.exchange(path, HttpMethod.POST, HttpEntity(body?.let { objectMapper.writeValueAsString(it) }, headers), String::class.java)
        return HttpStatus.valueOf(response.statusCode.value()) to response.body?.let { objectMapper.readTree(it) }
    }

    private fun createAndJoin(): Pair<String, String> {
        val (created, table) = post("/api/pokertable", mapOf("blindAmount" to 10))
        assertEquals(HttpStatus.CREATED, created)
        val tableId = table!!.path("tableId").asText()
        val (joined, session) = post("/api/pokertable/$tableId/join", mapOf("playerName" to "Alice"))
        assertEquals(HttpStatus.CREATED, joined)
        return tableId to session!!.path("sessionToken").asText()
    }

    private class Recorder : TextWebSocketHandler() {
        val messages = LinkedBlockingQueue<String>()
        val closed = CompletableFuture<CloseStatus>()

        override fun handleTextMessage(session: WebSocketSession, message: TextMessage) {
            messages.add(message.payload)
        }

        override fun afterConnectionClosed(session: WebSocketSession, status: CloseStatus) {
            closed.complete(status)
        }
    }

    private fun connect(tableId: String, recorder: Recorder): WebSocketSession =
        StandardWebSocketClient().execute(recorder, "ws://localhost:$port/api/pokertable/$tableId/ws").get(10, TimeUnit.SECONDS)

    @Test
    fun `the page's flow works with the token outside the URL`() {
        val (tableId, token) = createAndJoin()
        val recorder = Recorder()
        val socket = connect(tableId, recorder)

        socket.sendMessage(TextMessage("""{"type":"Auth","token":"$token"}"""))
        val first = objectMapper.readTree(recorder.messages.poll(10, TimeUnit.SECONDS))
        assertEquals("WaitingForPlayers", first.path("phase").asText())

        assertEquals(HttpStatus.NO_CONTENT, post("/api/pokertable/$tableId/start", null, token).first)
        assertNotNull(recorder.messages.poll(10, TimeUnit.SECONDS))
        socket.close()
    }

    @Test
    fun `a wrong Auth message closes the connection with 4001`() {
        val (tableId, _) = createAndJoin()
        val recorder = Recorder()
        val socket = connect(tableId, recorder)

        socket.sendMessage(TextMessage("""{"type":"Auth","token":"wrong"}"""))

        assertEquals(4001, recorder.closed.get(10, TimeUnit.SECONDS).code)
    }

    @Test
    fun `start needs a token and a table seats one checked human`() {
        val (tableId, _) = createAndJoin()

        // (a GET: the JDK client cannot read a 401 answer to a streamed POST)
        assertEquals(HttpStatus.UNAUTHORIZED.value(), rest.getForEntity("/api/pokertable/$tableId/snapshot", String::class.java).statusCode.value())
        assertEquals(HttpStatus.CONFLICT, post("/api/pokertable/$tableId/join", mapOf("playerName" to "Mallory")).first)
        assertEquals(HttpStatus.BAD_REQUEST, post("/api/pokertable/$tableId/join", mapOf("playerName" to " ")).first)
    }
}
