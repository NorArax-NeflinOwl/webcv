package com.pokerkotlin.core.model

/** Unique identifier for a player. Inline — no allocation overhead at runtime. */
@JvmInline
value class PlayerId(val value: String)
