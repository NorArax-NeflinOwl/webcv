package com.cvspringkotlin.controller

import com.cvspringkotlin.model.PandaGamePlayerScoreResponse
import com.cvspringkotlin.repository.PandaGamePlayerScoreRepository
import com.cvspringkotlin.service.PandaGamePlayerScoreService
import io.mockk.clearMocks
import io.mockk.every
import io.mockk.verify
import io.mockk.mockk
import org.junit.jupiter.api.BeforeEach
import org.junit.jupiter.api.Test
import org.springframework.beans.factory.annotation.Autowired
import org.springframework.boot.test.autoconfigure.web.servlet.WebMvcTest
import org.springframework.boot.test.context.TestConfiguration
import org.springframework.context.annotation.Bean
import org.springframework.http.MediaType
import org.springframework.test.web.servlet.MockMvc
import org.springframework.test.web.servlet.get
import org.springframework.test.web.servlet.post

@WebMvcTest(PandaGameController::class)
class PandaGamePlayerScoreControllerTest {

    @TestConfiguration
    class MockConfig {
        @Bean
        fun pandaGameService(): PandaGamePlayerScoreService = mockk()
    }

    @Autowired lateinit var mockMvc: MockMvc
    @Autowired lateinit var pandaGameService: PandaGamePlayerScoreService

    @BeforeEach
    fun setUp() {
        clearMocks(pandaGameService)
    }

    // ── POST /api/pandagame/scores — success ──────────────

    @Test
    fun `POST scores returns 201 and saved score`() {
        every { pandaGameService.saveScore(any()) } returns
                PandaGamePlayerScoreResponse(nick = "Player1", score = 123, playedAt = "2026-06-14T10:00:00")

        mockMvc.post("/api/pandagame/scores") {
            contentType = MediaType.APPLICATION_JSON
            content = """{"nick":"Player1","score":123}"""
        }.andExpect {
            status { isCreated() }
            content { contentType(MediaType.APPLICATION_JSON) }
            jsonPath("$.nick")  { value("Player1") }
            jsonPath("$.score") { value(123) }
        }

        verify(exactly = 1) { pandaGameService.saveScore(any()) }
    }

    // ── POST /api/pandagame/scores — validation errors ────

    @Test
    fun `POST scores returns 400 when service rejects data`() {
        every { pandaGameService.saveScore(any()) } throws IllegalArgumentException("Score cannot be negative")

        mockMvc.post("/api/pandagame/scores") {
            contentType = MediaType.APPLICATION_JSON
            content = """{"nick":"Player1","score":-5}"""
        }.andExpect {
            status { isBadRequest() }
            jsonPath("$.error") { value("Score cannot be negative") }
        }
    }

    @Test
    fun `POST scores returns 400 when JSON is malformed`() {
        mockMvc.post("/api/pandagame/scores") {
            contentType = MediaType.APPLICATION_JSON
            content = """{"nick": "Player1", "score": }"""
        }.andExpect {
            status { isBadRequest() }
            jsonPath("$.error") { exists() }
        }

        verify(exactly = 0) { pandaGameService.saveScore(any()) }
    }

    @Test
    fun `POST scores returns 500 when service throws unexpected error`() {
        every { pandaGameService.saveScore(any()) } throws RuntimeException("DB down")

        mockMvc.post("/api/pandagame/scores") {
            contentType = MediaType.APPLICATION_JSON
            content = """{"nick":"Player1","score":10}"""
        }.andExpect {
            status { isInternalServerError() }
            jsonPath("$.error") { value("Internal server error") }
        }
    }

    // ── GET /api/pandagame/scores ─────────────────────────

    @Test
    fun `GET scores returns 200 and list of scores`() {
        every { pandaGameService.topScores() } returns listOf(
            PandaGamePlayerScoreResponse(nick = "Player1", score = 200, playedAt = "2026-06-14T10:00:00"),
            PandaGamePlayerScoreResponse(nick = "Player2", score = 100, playedAt = "2026-06-14T09:00:00")
        )

        mockMvc.get("/api/pandagame/scores")
            .andExpect {
                status { isOk() }
                jsonPath("$") { isArray() }
                jsonPath("$[0].nick")  { value("Player1") }
                jsonPath("$[0].score") { value(200) }
                jsonPath("$[1].nick")  { value("Player2") }
            }
    }

    @Test
    fun `GET scores returns empty array when no scores exist`() {
        every { pandaGameService.topScores() } returns emptyList()

        mockMvc.get("/api/pandagame/scores")
            .andExpect {
                status { isOk() }
                jsonPath("$") { isArray() }
                jsonPath("$") { isEmpty() }
            }
    }
}
