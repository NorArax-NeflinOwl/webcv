package com.cvspringkotlin.model.entity

import jakarta.persistence.*
import java.time.LocalDateTime
import java.util.UUID

@Entity
@Table(name = "panda_game_player_score")
data class PandaGamePlayerScore(

    @Id
    @GeneratedValue(strategy = GenerationType.UUID)
    val id: UUID? = null,

    @Column(nullable = false, length = 64)
    val nick: String = "",

    @Column(nullable = false)
    val score: Int = 0,

    @Column(name = "played_at", nullable = false)
    val playedAt: LocalDateTime = LocalDateTime.now()
)