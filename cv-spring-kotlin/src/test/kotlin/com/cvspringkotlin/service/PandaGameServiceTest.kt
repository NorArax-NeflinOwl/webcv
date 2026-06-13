package com.cvspringkotlin.service

import com.cvspringkotlin.model.PandaGamePlayerScoreRequest
import com.cvspringkotlin.model.entity.PandaGamePlayerScore
import com.cvspringkotlin.repository.PandaGamePlayerScoreRepository
import io.mockk.*
import org.junit.jupiter.api.Assertions.*
import org.junit.jupiter.api.BeforeEach
import org.junit.jupiter.api.Test
import java.time.LocalDateTime

class PandaGameServiceTest {

    private val pandaScoreRepository: PandaGamePlayerScoreRepository = mockk()
    private val service = PandaGamePlayerScoreService(pandaScoreRepository)

    @BeforeEach
    fun setUp() {
        clearMocks(pandaScoreRepository)
    }

    // ── saveScore — przypadki poprawne ────────────────────

    @Test
    fun `saveScore zapisuje wynik z podanym nickiem`() {
        val request = PandaGamePlayerScoreRequest(nick = "Gracz1", score = 123)
        every { pandaScoreRepository.save(any()) } answers {
            (it.invocation.args[0] as PandaGamePlayerScore).copy(playedAt = LocalDateTime.of(2026, 6, 14, 10, 0))
        }

        val result = service.saveScore(request)

        assertEquals("Gracz1", result.nick)
        assertEquals(123, result.score)
        verify(exactly = 1) { pandaScoreRepository.save(match { it.nick == "Gracz1" && it.score == 123 }) }
    }

    @Test
    fun `saveScore przycina biale znaki w nicku`() {
        val request = PandaGamePlayerScoreRequest(nick = "  Gracz1  ", score = 10)
        every { pandaScoreRepository.save(any()) } answers { it.invocation.args[0] as PandaGamePlayerScore }

        val result = service.saveScore(request)

        assertEquals("Gracz1", result.nick)
    }

    @Test
    fun `saveScore uzywa Anonim gdy nick jest pusty`() {
        val request = PandaGamePlayerScoreRequest(nick = "   ", score = 50)
        every { pandaScoreRepository.save(any()) } answers { it.invocation.args[0] as PandaGamePlayerScore }

        val result = service.saveScore(request)

        assertEquals("Anonim", result.nick)
    }

    @Test
    fun `saveScore akceptuje wynik rowny zero`() {
        val request = PandaGamePlayerScoreRequest(nick = "Gracz", score = 0)
        every { pandaScoreRepository.save(any()) } answers { it.invocation.args[0] as PandaGamePlayerScore }

        val result = service.saveScore(request)

        assertEquals(0, result.score)
    }

    @Test
    fun `saveScore akceptuje wynik na granicy maksimum`() {
        val request = PandaGamePlayerScoreRequest(nick = "Gracz", score = PandaGamePlayerScoreService.MAX_SCORE)
        every { pandaScoreRepository.save(any()) } answers { it.invocation.args[0] as PandaGamePlayerScore }

        val result = service.saveScore(request)

        assertEquals(PandaGamePlayerScoreService.MAX_SCORE, result.score)
    }

    // ── saveScore — walidacja / błędy ─────────────────────

    @Test
    fun `saveScore odrzuca ujemny wynik`() {
        val request = PandaGamePlayerScoreRequest(nick = "Gracz", score = -1)

        assertThrows(IllegalArgumentException::class.java) { service.saveScore(request) }
        verify(exactly = 0) { pandaScoreRepository.save(any()) }
    }

    @Test
    fun `saveScore odrzuca wynik powyzej maksimum`() {
        val request = PandaGamePlayerScoreRequest(nick = "Gracz", score = PandaGamePlayerScoreService.MAX_SCORE + 1)

        assertThrows(IllegalArgumentException::class.java) { service.saveScore(request) }
        verify(exactly = 0) { pandaScoreRepository.save(any()) }
    }

    @Test
    fun `saveScore odrzuca zbyt dlugi nick`() {
        val request = PandaGamePlayerScoreRequest(nick = "x".repeat(PandaGamePlayerScoreService.MAX_NICK_LENGTH + 1), score = 10)

        assertThrows(IllegalArgumentException::class.java) { service.saveScore(request) }
        verify(exactly = 0) { pandaScoreRepository.save(any()) }
    }

    // ── topScores ──────────────────────────────────────────

    @Test
    fun `topScores zwraca zmapowane wyniki z repozytorium`() {
        val now = LocalDateTime.of(2026, 6, 14, 12, 0)
        every { pandaScoreRepository.findTop10ByOrderByScoreDescPlayedAtAsc() } returns listOf(
            PandaGamePlayerScore(nick = "Gracz1", score = 200, playedAt = now),
            PandaGamePlayerScore(nick = "Gracz2", score = 100, playedAt = now)
        )

        val result = service.topScores()

        assertEquals(2, result.size)
        assertEquals("Gracz1", result[0].nick)
        assertEquals(200, result[0].score)
        assertEquals("Gracz2", result[1].nick)
    }

    @Test
    fun `topScores zwraca pusta liste gdy brak wynikow`() {
        every { pandaScoreRepository.findTop10ByOrderByScoreDescPlayedAtAsc() } returns emptyList()

        val result = service.topScores()

        assertTrue(result.isEmpty())
    }
}
