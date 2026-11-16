package com.pokerkotlin.server.dto

/**
 * What a visitor may ask the poker API for. The API is public and anonymous, so every number and name
 * from a request is checked here before it reaches the engine (the same rules as cv-asp.net's PokerInput).
 */
object PokerInput {
    const val MAX_NAME_LENGTH = 24
    const val MIN_BLIND = 2
    const val MAX_BLIND = 1_000
    const val MIN_COINS = 1
    const val MAX_COINS = 100_000

    /** @return null when the blind is acceptable, otherwise the reason. */
    fun checkBlind(blindAmount: Int): String? =
        if (blindAmount in MIN_BLIND..MAX_BLIND) null else "blindAmount must be between $MIN_BLIND and $MAX_BLIND"

    /** @return null when the stack is acceptable, otherwise the reason. */
    fun checkCoins(coins: Int): String? =
        if (coins in MIN_COINS..MAX_COINS) null else "coins must be between $MIN_COINS and $MAX_COINS"

    /**
     * Trims the name and rejects an empty or too long one, or one with control or formatting characters
     * (line breaks, bidirectional overrides), which could disguise the name on the other players' pages.
     *
     * @return the trimmed name, or a failure with the reason.
     */
    fun checkName(playerName: String?): Result<String> {
        val name = playerName?.trim().orEmpty()
        val error = when {
            name.isEmpty() -> "playerName must not be empty"
            name.length > MAX_NAME_LENGTH -> "playerName cannot be longer than $MAX_NAME_LENGTH characters"
            name.any { Character.isISOControl(it) || Character.getType(it) == Character.FORMAT.toInt() } ->
                "playerName contains characters that are not allowed"
            else -> return Result.success(name)
        }
        return Result.failure(IllegalArgumentException(error))
    }
}
