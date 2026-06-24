package com.cvspringkotlin.model

data class PandaGamePlayerScoreRequest(
    val nick: String,
    val score: Int
)

data class PandaGamePlayerScoreResponse(
    val nick: String,
    val score: Int,
    val playedAt: String
)

