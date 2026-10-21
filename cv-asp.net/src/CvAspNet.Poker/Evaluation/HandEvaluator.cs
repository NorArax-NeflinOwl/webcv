using CvAspNet.Poker.Model;

namespace CvAspNet.Poker.Evaluation;

/// <summary>
/// Evaluates the best 5-card Texas Hold'em hand from a <see cref="Hand"/> of 5–7 cards.
/// <para>
/// Algorithm: generate all C(n, 5) five-card combinations (at most 21 for 7 cards), score each one
/// and return the highest. A five-card score detects the category from rank-group sizes and the
/// suit/straight checks, then builds an ordered tiebreaker list for within-category comparison.
/// </para>
/// </summary>
public static class HandEvaluator
{
    private const int CardsInHand = 5;
    private const int AceValue = (int)Rank.Ace;

    /// <exception cref="ArgumentException">The hand does not hold 5–7 cards.</exception>
    public static HandRank Evaluate(Hand hand)
    {
        var cards = hand.Cards;
        if (cards.Count is < 5 or > 7)
        {
            throw new ArgumentException($"HandEvaluator requires 5–7 cards, got {cards.Count}");
        }

        return Combinations(cards, CardsInHand).Select(EvaluateFive).Max()!;
    }

    private static HandRank EvaluateFive(IReadOnlyList<Card> cards)
    {
        // Larger groups first; within the same size, higher rank first.
        var groups = cards
            .GroupBy(card => (int)card.Rank)
            .OrderByDescending(group => group.Count())
            .ThenByDescending(group => group.Key)
            .ToList();

        var groupSizes = groups.Select(group => group.Count()).ToArray();  // e.g. [4,1], [3,2], [2,2,1]
        var groupRanks = groups.Select(group => group.Key).ToList();       // primary group first

        var ranksDescending = cards.Select(card => (int)card.Rank).OrderDescending().ToList();
        var isFlush = cards.Select(card => card.Suit).Distinct().Count() == 1;
        var isWheel = IsWheel(ranksDescending);
        var isStraight = IsStraight(ranksDescending);

        var category = (isFlush, isStraight) switch
        {
            (true, true) when ranksDescending[0] == AceValue && !isWheel => HandCategory.RoyalFlush,
            (true, true) => HandCategory.StraightFlush,
            _ when groupSizes is [4, 1] => HandCategory.FourOfAKind,
            _ when groupSizes is [3, 2] => HandCategory.FullHouse,
            (true, _) => HandCategory.Flush,
            (_, true) => HandCategory.Straight,
            _ when groupSizes is [3, 1, 1] => HandCategory.ThreeOfAKind,
            _ when groupSizes is [2, 2, 1] => HandCategory.TwoPair,
            _ when groupSizes is [2, 1, 1, 1] => HandCategory.OnePair,
            _ => HandCategory.HighCard,
        };

        IReadOnlyList<int> tiebreakers = category switch
        {
            // Straights: only the top card matters (the wheel's top card is the five, not the ace).
            HandCategory.Straight or HandCategory.StraightFlush or HandCategory.RoyalFlush =>
                [isWheel ? (int)Rank.Five : ranksDescending[0]],
            // Flush / high card: all five ranks, top down.
            HandCategory.Flush or HandCategory.HighCard => ranksDescending,
            // Grouped hands: primary group rank, then secondary, then kickers.
            _ => groupRanks,
        };

        return new HandRank(category, tiebreakers);
    }

    /// <summary>Five consecutive values, the wheel (A-2-3-4-5) included.</summary>
    private static bool IsStraight(IReadOnlyList<int> ranksDescending)
    {
        if (ranksDescending.Distinct().Count() != CardsInHand) return false;
        return ranksDescending[0] - ranksDescending[^1] == 4 || IsWheel(ranksDescending);
    }

    /// <summary>A-2-3-4-5, the lowest possible straight.</summary>
    private static bool IsWheel(IReadOnlyList<int> ranksDescending) =>
        ranksDescending.ToHashSet().SetEquals([AceValue, 5, 4, 3, 2]);

    /// <summary>All <paramref name="size"/>-element combinations; 21 for 7 cards choose 5 — negligible cost.</summary>
    private static List<IReadOnlyList<T>> Combinations<T>(IReadOnlyList<T> items, int size)
    {
        if (size == 0) return [[]];
        if (items.Count < size) return [];

        var head = items[0];
        var tail = items.Skip(1).ToList();
        var withHead = Combinations(tail, size - 1).Select(rest => (IReadOnlyList<T>)[head, .. rest]);
        return [.. withHead, .. Combinations(tail, size)];
    }
}
