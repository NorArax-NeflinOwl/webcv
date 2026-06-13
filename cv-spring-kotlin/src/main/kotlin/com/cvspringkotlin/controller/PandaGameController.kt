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


    // ── Obsługa błędów ────────────────────────────────────

    /** Niepoprawne dane wejściowe (np. ujemny wynik, zbyt długi nick). */
    @ExceptionHandler(IllegalArgumentException::class)
    fun handleInvalidInput(ex: IllegalArgumentException): ResponseEntity<ErrorResponse> {
        log.warn("Nieprawidłowe dane wyniku PandaGame: {}", ex.message)
        return ResponseEntity.badRequest().body(ErrorResponse(ex.message ?: "Nieprawidłowe dane"))
    }

    /** Brakujące lub niepoprawnie sformatowane JSON w żądaniu. */
    @ExceptionHandler(HttpMessageNotReadableException::class)
    fun handleMalformedJson(ex: HttpMessageNotReadableException): ResponseEntity<ErrorResponse> {
        log.warn("Niepoprawny JSON w żądaniu PandaGame: {}", ex.message)
        return ResponseEntity.badRequest().body(ErrorResponse("Niepoprawny format danych żądania"))
    }

    /** Wszystkie inne, nieprzewidziane błędy. */
    @ExceptionHandler(Exception::class)
    fun handleUnexpected(ex: Exception): ResponseEntity<ErrorResponse> {
        log.error("Nieoczekiwany błąd PandaGame", ex)
        return ResponseEntity.status(HttpStatus.INTERNAL_SERVER_ERROR)
            .body(ErrorResponse("Wewnętrzny błąd serwera"))
    }
}