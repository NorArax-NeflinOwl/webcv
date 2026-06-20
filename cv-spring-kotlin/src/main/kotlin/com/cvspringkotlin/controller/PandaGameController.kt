package com.cvspringkotlin.controller

import com.cvspringkotlin.model.ErrorResponse
import com.cvspringkotlin.model.PandaGamePlayerScoreResponse
import com.cvspringkotlin.model.PandaGamePlayerScoreRequest
import com.cvspringkotlin.service.PandaGamePlayerScoreService
import org.slf4j.LoggerFactory
import org.springframework.http.HttpStatus
import org.springframework.http.ResponseEntity
import org.springframework.http.converter.HttpMessageNotReadableException
import org.springframework.web.bind.annotation.*

@RestController
@RequestMapping("/api/pandagame")
class PandaGameController(private val pandaGameService: PandaGamePlayerScoreService) {

    private val log = LoggerFactory.getLogger(PandaGameController::class.java)

    @PostMapping("/scores")
    fun saveScore(@RequestBody request: PandaGamePlayerScoreRequest): ResponseEntity<PandaGamePlayerScoreResponse> {
        val saved = pandaGameService.saveScore(request)
        return ResponseEntity.status(HttpStatus.CREATED).body(saved)
    }

    @GetMapping("/scores")
    fun topScores(): ResponseEntity<List<PandaGamePlayerScoreResponse>> =
        ResponseEntity.ok(pandaGameService.topScores())


    // ── Error handling ────────────────────────────────────

    /** Invalid input data (e.g. negative score, nick too long). */
    @ExceptionHandler(IllegalArgumentException::class)
    fun handleInvalidInput(ex: IllegalArgumentException): ResponseEntity<ErrorResponse> {
        log.warn("Invalid PandaGame score data: {}", ex.message)
        return ResponseEntity.badRequest().body(ErrorResponse(ex.message ?: "Invalid data"))
    }

    /** Missing or malformed JSON in request. */
    @ExceptionHandler(HttpMessageNotReadableException::class)
    fun handleMalformedJson(ex: HttpMessageNotReadableException): ResponseEntity<ErrorResponse> {
        log.warn("Malformed JSON in PandaGame request: {}", ex.message)
        return ResponseEntity.badRequest().body(ErrorResponse("Invalid request data format"))
    }

    /** All other, unexpected errors. */
    @ExceptionHandler(Exception::class)
    fun handleUnexpected(ex: Exception): ResponseEntity<ErrorResponse> {
        log.error("Unexpected PandaGame error", ex)
        return ResponseEntity.status(HttpStatus.INTERNAL_SERVER_ERROR)
            .body(ErrorResponse("Internal server error"))
    }
}