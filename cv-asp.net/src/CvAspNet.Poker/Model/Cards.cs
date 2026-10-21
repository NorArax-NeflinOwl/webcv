namespace CvAspNet.Poker.Model;

/// <summary>Card ranks; the numeric value is used directly by hand evaluation (ace high = 14).</summary>
public enum Rank
{
    Two = 2,
    Three = 3,
    Four = 4,
    Five = 5,
    Six = 6,
    Seven = 7,
    Eight = 8,
    Nine = 9,
    Ten = 10,
    Jack = 11,
    Queen = 12,
    King = 13,
    Ace = 14,
}

/// <summary>The four-colour deck's suits, in the order a fresh deck is built.</summary>
public enum Suit
{
    GreenClub,
    BlueDiamond,
    RedHeart,
    BlackSpade,
}

public static class SuitExtensions
{
    /// <summary>One-letter symbol; breaks ties between equal ranks when cards are sorted.</summary>
    public static char Symbol(this Suit suit) => suit switch
    {
        Suit.GreenClub => 'G',
        Suit.BlueDiamond => 'B',
        Suit.RedHeart => 'R',
        Suit.BlackSpade => 'S',
        _ => throw new ArgumentOutOfRangeException(nameof(suit), suit, null),
    };
}

public sealed record Card(Rank Rank, Suit Suit) : IComparable<Card>
{
    public int CompareTo(Card? other)
    {
        if (other is null) return 1;
        var byRank = Rank.CompareTo(other.Rank);
        return byRank != 0 ? byRank : Suit.Symbol().CompareTo(other.Suit.Symbol());
    }

    public override string ToString() => $"{Rank} {Suit}";
}
