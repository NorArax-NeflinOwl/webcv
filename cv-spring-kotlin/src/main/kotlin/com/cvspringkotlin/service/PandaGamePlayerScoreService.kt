package com.cvspringkotlin.service

import com.cvspringkotlin.model.PandaGamePlayerScoreRequest
import com.cvspringkotlin.model.PandaGamePlayerScoreResponse
import com.cvspringkotlin.model.entity.PandaGamePlayerScore
import com.cvspringkotlin.repository.PandaGamePlayerScoreRepository
import org.slf4j.LoggerFactory
import org.springframework.stereotype.Service
import org.springframework.transaction.annotation.Transactional
import java.time.LocalDateTime
import java.time.ZoneId

@Service
class PandaGamePlayerScoreService(private val pandaScoreRepository: PandaGamePlayerScoreRepository) {

    private val log = LoggerFactory.getLogger(javaClass)
    private val POLISH_TIMEZONE = ZoneId.of("Europe/Warsaw")

    companion object {
        const val MAX_NICK_LENGTH = 64
        const val MAX_SCORE       = 1_000_000
        const val DEFAULT_NICK    = "Anonymous"
    }

    @Transactional
    fun saveScore(request: PandaGamePlayerScoreRequest): PandaGamePlayerScoreResponse {
        val nick = request.nick.trim().ifBlank { DEFAULT_NICK }

        require(nick.length <= MAX_NICK_LENGTH) {
            "Nick cannot be longer then $MAX_NICK_LENGTH chars"
        }
        require(request.score >= 0) {
            "Score cnnot be negative"
        }
        require(request.score <= MAX_SCORE) {
            "Score is higher then maximum value ($MAX_SCORE)"
        }

        val saved = pandaScoreRepository.save(
            PandaGamePlayerScore(nick = nick, score = request.score, playedAt = LocalDateTime.now(POLISH_TIMEZONE))
        )

        if(log.isDebugEnabled) {
            log.debug("Saved Panda score for nick=$nick, score=${request.score}")
        }

        return saved.toDto()
    }

    @Transactional(readOnly = true)
    fun topScores(): List<PandaGamePlayerScoreResponse> =
        pandaScoreRepository.findTop10ByOrderByScoreDescPlayedAtAsc().map { it.toDto() }

    private fun PandaGamePlayerScore.toDto() = PandaGamePlayerScoreResponse(nick = nick, score = score, playedAt = playedAt.toString())
}