package com.pokerkotlin.core.model

/** Unique identifier for a poker table. Inline — no allocation overhead at runtime. */
@JvmInline
value class TableId(val value: String)
