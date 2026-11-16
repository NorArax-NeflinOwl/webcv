using System.Globalization;

namespace CvAspNet.Web.Poker;

/// <summary>
/// What a visitor may ask the poker API for. The API is public and anonymous, so every number and name
/// from a request is checked here before it reaches the engine.
/// </summary>
public static class PokerInput
{
    public const int MaxNameLength = 24;
    public const int MinBlind = 2;
    public const int MaxBlind = 1_000;
    public const int MinCoins = 1;
    public const int MaxCoins = 100_000;

    /// <returns>Null when the blind is acceptable, otherwise the reason.</returns>
    public static string? CheckBlind(int blindAmount) =>
        blindAmount is >= MinBlind and <= MaxBlind ? null : $"blindAmount must be between {MinBlind} and {MaxBlind}";

    /// <returns>Null when the stack is acceptable, otherwise the reason.</returns>
    public static string? CheckCoins(int coins) =>
        coins is >= MinCoins and <= MaxCoins ? null : $"coins must be between {MinCoins} and {MaxCoins}";

    /// <summary>
    /// Trims the name and rejects an empty or too long one, or one with control or formatting characters
    /// (line breaks, bidirectional overrides), which could disguise the name on the other players' pages.
    /// </summary>
    /// <param name="name">The trimmed name when it is acceptable.</param>
    /// <returns>Null when the name is acceptable, otherwise the reason.</returns>
    public static string? CheckName(string? playerName, out string name)
    {
        name = playerName?.Trim() ?? "";
        if (name.Length == 0) return "playerName must not be empty";
        if (name.Length > MaxNameLength) return $"playerName cannot be longer than {MaxNameLength} characters";
        if (name.Any(character => char.IsControl(character) || CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.Format))
        {
            return "playerName contains characters that are not allowed";
        }
        return null;
    }
}
