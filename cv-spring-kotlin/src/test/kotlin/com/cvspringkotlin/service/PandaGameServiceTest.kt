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

    // ── saveScore — happy path ────────────────────────────

    @Test
    fun `saveScore saves score with given nick`() {
        val request = PandaGamePlayerScoreRequest(nick = "Player1", score = 123)
        every { pandaScoreRepository.save(any()) } answers {
            (it.invocation.args[0] as PandaGamePlayerScore).copy(playedAt = LocalDateTime.of(2026, 6, 14, 10, 0))
        }

        val result = service.saveScore(request)

        assertEquals("Player1", result.nick)
        assertEquals(123, result.score)
        verify(exactly = 1) { pandaScoreRepository.save(match { it.nick == "Player1" && it.score == 123 }) }
    }

    @Test
    fun `saveScore trims whitespace from nick`() {
        val request = PandaGamePlayerScoreRequest(nick = "  Player1  ", score = 10)
        every { pandaScoreRepository.save(any()) } answers { it.invocation.args[0] as PandaGamePlayerScore }

        val result = service.saveScore(request)

        assertEquals("Player1", result.nick)
    }

    @Test
    fun `saveScore uses Anonymous when nick is blank`() {
        val request = PandaGamePlayerScoreRequest(nick = "   ", score = 50)
        every { pandaScoreRepository.save(any()) } answers { it.invocation.args[0] as PandaGamePlayerScore }

        val result = service.saveScore(request)

        assertEquals("Anonymous", result.nick)
    }

    @Test
    fun `saveScore accepts score equal to zero`() {
        val request = PandaGamePlayerScoreRequest(nick = "Player", score = 0)
        every { pandaScoreRepository.save(any()) } answers { it.invocation.args[0] as PandaGamePlayerScore }

        val result = service.saveScore(request)

        assertEquals(0, result.score)
    }

    @Test
    fun `saveScore accepts score at maximum boundary`() {
        val request = PandaGamePlayerScoreRequest(nick = "Player", score = PandaGamePlayerScoreService.MAX_SCORE)
        every { pandaScoreRepository.save(any()) } answers { it.invocation.args[0] as PandaGamePlayerScore }

        val result = service.saveScore(request)

        assertEquals(PandaGamePlayerScoreService.MAX_SCORE, result.score)
    }

    // ── saveScore — validation / errors ───────────────────

    @Test
    fun `saveScore rejects negative score`() {
        val request = PandaGamePlayerScoreRequest(nick = "Player", score = -1)

        assertThrows(IllegalArgumentException::class.java) { service.saveScore(request) }
        verify(exactly = 0) { pandaScoreRepository.save(any()) }
    }

    @Test
    fun `saveScore rejects score above maximum`() {
        val request = PandaGamePlayerScoreRequest(nick = "Player", score = PandaGamePlayerScoreService.MAX_SCORE + 1)

        assertThrows(IllegalArgumentException::class.java) { service.saveScore(request) }
        verify(exactly = 0) { pandaScoreRepository.save(any()) }
    }

    @Test
    fun `saveScore rejects nick that is too long`() {
        val request = PandaGamePlayerScoreRequest(nick = "x".repeat(PandaGamePlayerScoreService.MAX_NICK_LENGTH + 1), score = 10)

        assertThrows(IllegalArgumentException::class.java) { service.saveScore(request) }
        verify(exactly = 0) { pandaScoreRepository.save(any()) }
    }

    // ── topScores ──────────────────────────────────────────

    @Test
    fun `topScores returns mapped scores from repository`() {
        val now = LocalDateTime.of(2026, 6, 14, 12, 0)
        every { pandaScoreRepository.findTop10ByOrderByScoreDescPlayedAtAsc() } returns listOf(
            PandaGamePlayerScore(nick = "Player1", score = 200, playedAt = now),
            PandaGamePlayerScore(nick = "Player2", score = 100, playedAt = now)
        )

        val result = service.topScores()

        assertEquals(2, result.size)
        assertEquals("Player1", result[0].nick)
        assertEquals(200, result[0].score)
        assertEquals("Player2", result[1].nick)
    }

    @Test
    fun `topScores returns empty list when no scores exist`() {
        every { pandaScoreRepository.findTop10ByOrderByScoreDescPlayedAtAsc() } returns emptyList()

        val result = service.topScores()

        assertTrue(result.isEmpty())
    }
}
