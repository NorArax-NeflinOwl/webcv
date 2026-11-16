package com.pokerkotlin.server.dto

import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertNotNull
import org.junit.jupiter.api.Assertions.assertNull
import org.junit.jupiter.api.Assertions.assertTrue
import org.junit.jupiter.api.Test

class PokerInputTest {

    @Test
    fun `checkName trims and keeps markup as text`() {
        assertEquals("<b>Alice</b>", PokerInput.checkName("  <b>Alice</b>  ").getOrThrow())
    }

    @Test
    fun `checkName rejects empty, long, control and formatting characters`() {
        listOf("", "   ", "A name that is much too long for a seat", "Line\nbreak", "Evil‮eman")
            .forEach { assertTrue(PokerInput.checkName(it).isFailure, it) }
    }

    @Test
    fun `checkBlind and checkCoins accept only their ranges`() {
        assertNull(PokerInput.checkBlind(10))
        listOf(1, -10, PokerInput.MAX_BLIND + 1).forEach { assertNotNull(PokerInput.checkBlind(it)) }
        assertNull(PokerInput.checkCoins(1_000))
        listOf(0, -1, Int.MAX_VALUE).forEach { assertNotNull(PokerInput.checkCoins(it)) }
    }
}
