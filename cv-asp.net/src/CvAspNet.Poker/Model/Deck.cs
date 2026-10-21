using System.Security.Cryptography;

namespace CvAspNet.Poker.Model;

public sealed class Deck
{
    private readonly List<Card> cards;

    private Deck(List<Card> cards) => this.cards = cards;

    public int Remaining => cards.Count;

    public Card Draw()
    {
        if (cards.Count == 0) throw new InvalidOperationException("Deck is empty");
        var card = cards[0];
        cards.RemoveAt(0);
        return card;
    }

    public IReadOnlyList<Card> DrawMany(int count) => Enumerable.Range(0, count).Select(_ => Draw()).ToList();

    /// <summary>A freshly shuffled full 52-card deck (Fisher-Yates over a cryptographic random source).</summary>
    public static Deck Shuffle()
    {
        var all = FreshDeck().ToList();
        for (var i = all.Count - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (all[i], all[j]) = (all[j], all[i]);
        }
        return new Deck(all);
    }

    /// <summary>
    /// A deck holding exactly the given cards, in the given order.
    /// Intended for tests: lets callers control exactly which cards are dealt.
    /// </summary>
    public static Deck Of(params Card[] cards) => new([.. cards]);

    /// <summary>All 52 cards, suit by suit, each suit from two to ace.</summary>
    public static IReadOnlyList<Card> FreshDeck() =>
        Enum.GetValues<Suit>().SelectMany(suit => Enum.GetValues<Rank>().Select(rank => new Card(rank, suit))).ToList();
}
