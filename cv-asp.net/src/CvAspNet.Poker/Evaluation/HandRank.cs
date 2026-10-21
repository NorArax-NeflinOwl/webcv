using CvAspNet.Poker.Model;

namespace CvAspNet.Poker.Evaluation;

/// <summary>
/// Poker hand rankings ordered from lowest to highest.
/// The numeric value is used directly for comparison — do not reorder entries.
/// </summary>
public enum HandCategory
{
    HighCard,
    OnePair,
    TwoPair,
    ThreeOfAKind,
    Straight,
    Flush,
    FullHouse,
    FourOfAKind,
    StraightFlush,
    RoyalFlush,
}

public static class HandCategoryExtensions
{
    public static string DisplayName(this HandCategory category) => category switch
    {
        HandCategory.HighCard => "High Card",
        HandCategory.OnePair => "One Pair",
        HandCategory.TwoPair => "Two Pair",
        HandCategory.ThreeOfAKind => "Three of a Kind",
        HandCategory.Straight => "Straight",
        HandCategory.Flush => "Flush",
        HandCategory.FullHouse => "Full House",
        HandCategory.FourOfAKind => "Four of a Kind",
        HandCategory.StraightFlush => "Straight Flush",
        HandCategory.RoyalFlush => "Royal Flush",
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, null),
    };
}

/// <summary>
/// The evaluated rank of a 5-card poker hand.
/// </summary>
/// <param name="Category">The hand category (pair, flush, etc.).</param>
/// <param name="Tiebreakers">
/// Card values that break ties within the same category, most significant first
/// (e.g. the pair's rank, then the kickers in descending order).
/// </param>
public sealed class HandRank(HandCategory category, IReadOnlyList<int> tiebreakers) : IComparable<HandRank>
{
    public HandCategory Category { get; } = category;

    public IReadOnlyList<int> Tiebreakers { get; } = tiebreakers;

    /// <summary>Compares by category first, then by tiebreakers left to right; positive when this hand is stronger.</summary>
    public int CompareTo(HandRank? other)
    {
        if (other is null) return 1;

        var categoryDifference = Category - other.Category;
        if (categoryDifference != 0) return categoryDifference;

        foreach (var (mine, theirs) in Tiebreakers.Zip(other.Tiebreakers))
        {
            if (mine != theirs) return mine - theirs;
        }
        return 0;
    }

    public static bool operator >(HandRank left, HandRank right) => left.CompareTo(right) > 0;

    public static bool operator <(HandRank left, HandRank right) => left.CompareTo(right) < 0;

    public override string ToString() => $"{Category.DisplayName()} [{string.Join(", ", Tiebreakers)}]";
}

/// <summary>
/// A player's hand at showdown. <see cref="HandValue"/> holds the full evaluation hand
/// (hole cards + board), so results sort by hand strength — strongest compares greatest.
/// </summary>
public sealed record PlayerResult(PlayerId PlayerId, string PlayerName, Hand HandValue) : IComparable<PlayerResult>
{
    public int CompareTo(PlayerResult? other) => other is null ? 1 : HandValue.CompareTo(other.HandValue);
}
