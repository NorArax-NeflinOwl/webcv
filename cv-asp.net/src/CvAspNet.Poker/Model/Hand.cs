using CvAspNet.Poker.Evaluation;

namespace CvAspNet.Poker.Model;

/// <summary>
/// A set of cards: either a player's hole cards (2 cards) or a full evaluation hand
/// (5–7 cards: hole cards + board).
/// <see cref="CompareTo"/> is only valid for evaluation hands and delegates to <see cref="HandEvaluator"/>;
/// do not compare 2-card hole hands.
/// </summary>
public sealed class Hand(IReadOnlyList<Card> cards) : IComparable<Hand>
{
    public static Hand Empty { get; } = new([]);

    public IReadOnlyList<Card> Cards { get; } = cards;

    public int CompareTo(Hand? other) =>
        other is null ? 1 : HandEvaluator.Evaluate(this).CompareTo(HandEvaluator.Evaluate(other));

    public override string ToString() => $"[{string.Join(", ", Cards)}]";
}
