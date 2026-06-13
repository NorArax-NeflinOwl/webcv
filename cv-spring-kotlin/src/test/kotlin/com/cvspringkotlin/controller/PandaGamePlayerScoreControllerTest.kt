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

    // ── POST /api/pandagame/scores — sukces ───────────────

    @Test
    fun `POST scores zwraca 201 i zapisany wynik`() {
        every { pandaGameService.saveScore(any()) } returns
                PandaGamePlayerScoreResponse(nick = "Gracz1", score = 123, playedAt = "2026-06-14T10:00:00")

        mockMvc.post("/api/pandagame/scores") {
            contentType = MediaType.APPLICATION_JSON
            content = """{"nick":"Gracz1","score":123}"""
        }.andExpect {
            status { isCreated() }
            content { contentType(MediaType.APPLICATION_JSON) }
            jsonPath("$.nick")  { value("Gracz1") }
            jsonPath("$.score") { value(123) }
        }

        verify(exactly = 1) { pandaGameService.saveScore(any()) }
    }

    // ── POST /api/pandagame/scores — błędy walidacji ──────

    @Test
    fun `POST scores zwraca 400 gdy serwis odrzuca dane`() {
        every { pandaGameService.saveScore(any()) } throws IllegalArgumentException("Wynik nie może być ujemny")

        mockMvc.post("/api/pandagame/scores") {
            contentType = MediaType.APPLICATION_JSON
            content = """{"nick":"Gracz1","score":-5}"""
        }.andExpect {
            status { isBadRequest() }
            jsonPath("$.error") { value("Wynik nie może być ujemny") }
        }
    }

    @Test
    fun `POST scores zwraca 400 gdy JSON jest niepoprawny`() {
        mockMvc.post("/api/pandagame/scores") {
            contentType = MediaType.APPLICATION_JSON
            content = """{"nick": "Gracz1", "score": }"""
        }.andExpect {
            status { isBadRequest() }
            jsonPath("$.error") { exists() }
        }

        verify(exactly = 0) { pandaGameService.saveScore(any()) }
    }

    @Test
    fun `POST scores zwraca 500 gdy serwis zglasza nieoczekiwany blad`() {
        every { pandaGameService.saveScore(any()) } throws RuntimeException("DB down")

        mockMvc.post("/api/pandagame/scores") {
            contentType = MediaType.APPLICATION_JSON
            content = """{"nick":"Gracz1","score":10}"""
        }.andExpect {
            status { isInternalServerError() }
            jsonPath("$.error") { value("Wewnętrzny błąd serwera") }
        }
    }

    // ── GET /api/pandagame/scores ─────────────────────────

    @Test
    fun `GET scores zwraca 200 i liste wynikow`() {
        every { pandaGameService.topScores() } returns listOf(
            PandaGamePlayerScoreResponse(nick = "Gracz1", score = 200, playedAt = "2026-06-14T10:00:00"),
            PandaGamePlayerScoreResponse(nick = "Gracz2", score = 100, playedAt = "2026-06-14T09:00:00")
        )

        mockMvc.get("/api/pandagame/scores")
            .andExpect {
                status { isOk() }
                jsonPath("$") { isArray() }
                jsonPath("$[0].nick")  { value("Gracz1") }
                jsonPath("$[0].score") { value(200) }
                jsonPath("$[1].nick")  { value("Gracz2") }
            }
    }

    @Test
    fun `GET scores zwraca pusta tablice gdy brak wynikow`() {
        every { pandaGameService.topScores() } returns emptyList()

        mockMvc.get("/api/pandagame/scores")
            .andExpect {
                status { isOk() }
                jsonPath("$") { isArray() }
                jsonPath("$") { isEmpty() }
            }
    }
}
