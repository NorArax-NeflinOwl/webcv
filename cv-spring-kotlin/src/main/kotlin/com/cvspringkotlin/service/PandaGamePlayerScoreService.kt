package com.cvspringkotlin.service

import com.cvspringkotlin.model.PandaGamePlayerScoreRequest
import com.cvspringkotlin.model.PandaGamePlayerScoreResponse
import com.cvspringkotlin.model.entity.PandaGamePlayerScore
import com.cvspringkotlin.repository.PandaGamePlayerScoreRepository
import org.springframework.stereotype.Service
import org.springframework.transaction.annotation.Transactional
import java.time.LocalDateTime

@Service
class PandaGamePlayerScoreService(private val pandaScoreRepository: PandaGamePlayerScoreRepository) {

    companion object {
        const val MAX_NICK_LENGTH = 64
        const val MAX_SCORE       = 1_000_000
        const val DEFAULT_NICK    = "Anonim"
    }

    @Transactional
    fun saveScore(request: PandaGamePlayerScoreRequest): PandaGamePlayerScoreResponse {
        val nick = request.nick.trim().ifBlank { DEFAULT_NICK }

        require(nick.length <= MAX_NICK_LENGTH) {
            "Nick nie może być dłuższy niż $MAX_NICK_LENGTH znaków"
        }
        require(request.score >= 0) {
            "Wynik nie może być ujemny"
        }
        require(request.score <= MAX_SCORE) {
            "Wynik przekracza maksymalną dozwoloną wartość ($MAX_SCORE)"
        }

        val saved = pandaScoreRepository.save(
            PandaGamePlayerScore(nick = nick, score = request.score, playedAt = LocalDateTime.now())
        )

        return saved.toDto()
    }

    @Transactional(readOnly = true)
    fun topScores(): List<PandaGamePlayerScoreResponse> =
        pandaScoreRepository.findTop10ByOrderByScoreDescPlayedAtAsc().map { it.toDto() }

    private fun PandaGamePlayerScore.toDto() = PandaGamePlayerScoreResponse(nick = nick, score = score, playedAt = playedAt.toString())
}