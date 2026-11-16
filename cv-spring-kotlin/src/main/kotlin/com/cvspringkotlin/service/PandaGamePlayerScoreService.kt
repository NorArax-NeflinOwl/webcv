package com.cvspringkotlin.service

import com.cvspringkotlin.model.PandaGamePlayerScoreRequest
import com.cvspringkotlin.model.PandaGamePlayerScoreResponse
import com.cvspringkotlin.model.entity.PandaGamePlayerScore
import com.cvspringkotlin.repository.PandaGamePlayerScoreRepository
import org.slf4j.LoggerFactory
import org.springframework.data.domain.PageRequest
import org.springframework.stereotype.Service
import org.springframework.transaction.annotation.Transactional
import java.time.LocalDateTime
import java.time.ZoneId

@Service
class PandaGamePlayerScoreService(private val pandaScoreRepository: PandaGamePlayerScoreRepository) {

    private val log = LoggerFactory.getLogger(javaClass)

    companion object {
        /** The page's nick field takes 20 characters. */
        const val MAX_NICK_LENGTH = 24
        /**
         * The game adds a point per frame: 100 000 is about half an hour of flawless play at 60 fps, at a speed
         * that grows with every 100 points. A higher score was not played, and the table is public.
         */
        const val MAX_SCORE       = 100_000
        const val DEFAULT_NICK    = "Anonymous"
        /** Only the best scores are kept, so that anonymous saves cannot fill the disk. */
        const val MAX_STORED_SCORES = 1_000
        val POLISH_TIMEZONE: ZoneId = ZoneId.of("Europe/Warsaw")
    }

    /** Saves the score, then keeps only the best [MAX_STORED_SCORES]. */
    @Transactional
    fun saveScore(request: PandaGamePlayerScoreRequest): PandaGamePlayerScoreResponse {
        val nick = request.nick.trim().ifBlank { DEFAULT_NICK }

        require(nick.length <= MAX_NICK_LENGTH) {
            "Nick cannot be longer than $MAX_NICK_LENGTH characters"
        }
        require(nick.none { Character.isISOControl(it) || Character.getType(it) == Character.FORMAT.toInt() }) {
            "Nick contains characters that are not allowed"
        }
        require(request.score >= 0) {
            "Score cannot be negative"
        }
        require(request.score <= MAX_SCORE) {
            "Score cannot exceed $MAX_SCORE"
        }

        val saved = pandaScoreRepository.save(
            PandaGamePlayerScore(nick = nick, score = request.score, playedAt = LocalDateTime.now(POLISH_TIMEZONE))
        )

        log.debug("Saved Panda score for nick={}, score={}", nick, request.score)

        deleteAllButTop(MAX_STORED_SCORES)
        return saved.toDto()
    }

    /** Deletes every score outside the best [keep], the worst first (the reverse of the leaderboard's order). */
    private fun deleteAllButTop(keep: Int) {
        val excess = pandaScoreRepository.count() - keep
        if (excess <= 0) return
        val worst = pandaScoreRepository.findAllByOrderByScoreAscPlayedAtDesc(PageRequest.of(0, excess.toInt()))
        pandaScoreRepository.deleteAll(worst)
        log.debug("Pruned {} Panda scores below the best {}", worst.size, keep)
    }

    @Transactional(readOnly = true)
    fun topScores(): List<PandaGamePlayerScoreResponse> =
        pandaScoreRepository.findTop10ByOrderByScoreDescPlayedAtAsc().map { it.toDto() }

    private fun PandaGamePlayerScore.toDto() = PandaGamePlayerScoreResponse(nick = nick, score = score, playedAt = playedAt.toString())
}