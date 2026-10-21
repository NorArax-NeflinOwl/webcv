using CvAspNet.Poker.Evaluation;
using CvAspNet.Poker.Model;

namespace CvAspNet.Tests.Poker;

public class HandEvaluatorTests
{
    private static Card C(Rank rank, Suit suit) => new(rank, suit);

    private static Hand H(params Card[] cards) => new(cards);

    // ── All 10 hand categories ──

    [Fact]
    public void HighCard_NoMatchingPattern()
    {
        var rank = HandEvaluator.Evaluate(H(
            C(Rank.Two, Suit.RedHeart), C(Rank.Four, Suit.BlackSpade), C(Rank.Six, Suit.GreenClub),
            C(Rank.Eight, Suit.BlueDiamond), C(Rank.Ten, Suit.RedHeart)));

        Assert.Equal(HandCategory.HighCard, rank.Category);
        Assert.Equal([10, 8, 6, 4, 2], rank.Tiebreakers);
    }

    [Fact]
    public void OnePair()
    {
        var rank = HandEvaluator.Evaluate(H(
            C(Rank.Ace, Suit.RedHeart), C(Rank.Ace, Suit.BlackSpade), C(Rank.King, Suit.GreenClub),
            C(Rank.Queen, Suit.BlueDiamond), C(Rank.Jack, Suit.RedHeart)));

        Assert.Equal(HandCategory.OnePair, rank.Category);
        Assert.Equal([14, 13, 12, 11], rank.Tiebreakers);
    }

    [Fact]
    public void TwoPair()
    {
        var rank = HandEvaluator.Evaluate(H(
            C(Rank.Ace, Suit.RedHeart), C(Rank.Ace, Suit.BlackSpade), C(Rank.King, Suit.GreenClub),
            C(Rank.King, Suit.BlueDiamond), C(Rank.Queen, Suit.RedHeart)));

        Assert.Equal(HandCategory.TwoPair, rank.Category);
        Assert.Equal([14, 13, 12], rank.Tiebreakers);
    }

    [Fact]
    public void ThreeOfAKind()
    {
        var rank = HandEvaluator.Evaluate(H(
            C(Rank.Ace, Suit.RedHeart), C(Rank.Ace, Suit.BlackSpade), C(Rank.Ace, Suit.GreenClub),
            C(Rank.King, Suit.BlueDiamond), C(Rank.Queen, Suit.RedHeart)));

        Assert.Equal(HandCategory.ThreeOfAKind, rank.Category);
        Assert.Equal(14, rank.Tiebreakers[0]);
    }

    [Fact]
    public void Straight_TenToAce()
    {
        var rank = HandEvaluator.Evaluate(H(
            C(Rank.Ten, Suit.RedHeart), C(Rank.Jack, Suit.BlackSpade), C(Rank.Queen, Suit.GreenClub),
            C(Rank.King, Suit.BlueDiamond), C(Rank.Ace, Suit.RedHeart)));

        Assert.Equal(HandCategory.Straight, rank.Category);
        Assert.Equal([14], rank.Tiebreakers);
    }

    [Fact]
    public void WheelStraight_TopCardIsFiveNotAce()
    {
        var rank = HandEvaluator.Evaluate(H(
            C(Rank.Ace, Suit.RedHeart), C(Rank.Two, Suit.BlackSpade), C(Rank.Three, Suit.GreenClub),
            C(Rank.Four, Suit.BlueDiamond), C(Rank.Five, Suit.RedHeart)));

        Assert.Equal(HandCategory.Straight, rank.Category);
        Assert.Equal([5], rank.Tiebreakers);
    }

    [Fact]
    public void Flush()
    {
        var rank = HandEvaluator.Evaluate(H(
            C(Rank.Two, Suit.RedHeart), C(Rank.Five, Suit.RedHeart), C(Rank.Seven, Suit.RedHeart),
            C(Rank.Nine, Suit.RedHeart), C(Rank.Jack, Suit.RedHeart)));

        Assert.Equal(HandCategory.Flush, rank.Category);
        Assert.Equal([11, 9, 7, 5, 2], rank.Tiebreakers);
    }

    [Fact]
    public void FullHouse()
    {
        var rank = HandEvaluator.Evaluate(H(
            C(Rank.Ace, Suit.RedHeart), C(Rank.Ace, Suit.BlackSpade), C(Rank.Ace, Suit.GreenClub),
            C(Rank.King, Suit.BlueDiamond), C(Rank.King, Suit.RedHeart)));

        Assert.Equal(HandCategory.FullHouse, rank.Category);
        Assert.Equal([14, 13], rank.Tiebreakers);
    }

    [Fact]
    public void FourOfAKind()
    {
        var rank = HandEvaluator.Evaluate(H(
            C(Rank.Ace, Suit.RedHeart), C(Rank.Ace, Suit.BlackSpade), C(Rank.Ace, Suit.GreenClub),
            C(Rank.Ace, Suit.BlueDiamond), C(Rank.King, Suit.RedHeart)));

        Assert.Equal(HandCategory.FourOfAKind, rank.Category);
        Assert.Equal([14, 13], rank.Tiebreakers);
    }

    [Fact]
    public void StraightFlush()
    {
        var rank = HandEvaluator.Evaluate(H(
            C(Rank.Five, Suit.RedHeart), C(Rank.Six, Suit.RedHeart), C(Rank.Seven, Suit.RedHeart),
            C(Rank.Eight, Suit.RedHeart), C(Rank.Nine, Suit.RedHeart)));

        Assert.Equal(HandCategory.StraightFlush, rank.Category);
        Assert.Equal([9], rank.Tiebreakers);
    }

    [Fact]
    public void RoyalFlush()
    {
        var rank = HandEvaluator.Evaluate(H(
            C(Rank.Ten, Suit.RedHeart), C(Rank.Jack, Suit.RedHeart), C(Rank.Queen, Suit.RedHeart),
            C(Rank.King, Suit.RedHeart), C(Rank.Ace, Suit.RedHeart)));

        Assert.Equal(HandCategory.RoyalFlush, rank.Category);
    }

    // ── 7-card best-5 selection ──

    [Fact]
    public void SevenCards_PicksTheBestFiveCardCombination()
    {
        var rank = HandEvaluator.Evaluate(H(
            C(Rank.Ten, Suit.RedHeart), C(Rank.Jack, Suit.RedHeart), C(Rank.Queen, Suit.RedHeart),
            C(Rank.King, Suit.RedHeart), C(Rank.Ace, Suit.RedHeart),
            C(Rank.Two, Suit.BlackSpade), C(Rank.Three, Suit.GreenClub)));

        Assert.Equal(HandCategory.RoyalFlush, rank.Category);
    }

    [Fact]
    public void SevenCards_SelectsFullHouseOverPair()
    {
        var rank = HandEvaluator.Evaluate(H(
            C(Rank.King, Suit.RedHeart), C(Rank.King, Suit.BlackSpade), C(Rank.King, Suit.GreenClub),
            C(Rank.Queen, Suit.BlueDiamond), C(Rank.Queen, Suit.RedHeart),
            C(Rank.Two, Suit.GreenClub), C(Rank.Three, Suit.BlueDiamond)));

        Assert.Equal(HandCategory.FullHouse, rank.Category);
        Assert.Equal([13, 12], rank.Tiebreakers);
    }

    // ── Tiebreakers ──

    [Fact]
    public void KickerDecidesBetweenEqualPairsOfAces()
    {
        var highKicker = H(
            C(Rank.Ace, Suit.RedHeart), C(Rank.Ace, Suit.BlackSpade), C(Rank.King, Suit.GreenClub),
            C(Rank.Queen, Suit.BlueDiamond), C(Rank.Jack, Suit.RedHeart));
        var lowKicker = H(
            C(Rank.Ace, Suit.GreenClub), C(Rank.Ace, Suit.BlueDiamond), C(Rank.Nine, Suit.RedHeart),
            C(Rank.Eight, Suit.BlackSpade), C(Rank.Seven, Suit.GreenClub));

        Assert.True(HandEvaluator.Evaluate(highKicker) > HandEvaluator.Evaluate(lowKicker));
    }

    [Fact]
    public void TwoFlushes_HigherKickerWins()
    {
        var strongFlush = H(
            C(Rank.Ace, Suit.GreenClub), C(Rank.King, Suit.GreenClub), C(Rank.Queen, Suit.GreenClub),
            C(Rank.Jack, Suit.GreenClub), C(Rank.Nine, Suit.GreenClub));
        var weakFlush = H(
            C(Rank.Ace, Suit.BlueDiamond), C(Rank.King, Suit.BlueDiamond), C(Rank.Queen, Suit.BlueDiamond),
            C(Rank.Jack, Suit.BlueDiamond), C(Rank.Eight, Suit.BlueDiamond));

        Assert.True(HandEvaluator.Evaluate(strongFlush) > HandEvaluator.Evaluate(weakFlush));
    }

    [Fact]
    public void IdenticalHandsCompareAsEqual()
    {
        Hand FourAces() => H(
            C(Rank.Ace, Suit.RedHeart), C(Rank.Ace, Suit.BlackSpade), C(Rank.Ace, Suit.GreenClub),
            C(Rank.Ace, Suit.BlueDiamond), C(Rank.King, Suit.RedHeart));

        Assert.Equal(0, HandEvaluator.Evaluate(FourAces()).CompareTo(HandEvaluator.Evaluate(FourAces())));
    }

    // ── Validation ──

    [Fact]
    public void Error_WhenTooFewCards()
    {
        Assert.Throws<ArgumentException>(() => HandEvaluator.Evaluate(H(C(Rank.Ace, Suit.RedHeart), C(Rank.King, Suit.BlackSpade))));
    }

    [Fact]
    public void Error_WhenTooManyCards()
    {
        var hand = H(
            C(Rank.Ace, Suit.RedHeart), C(Rank.King, Suit.BlackSpade), C(Rank.Queen, Suit.GreenClub), C(Rank.Jack, Suit.BlueDiamond),
            C(Rank.Ten, Suit.RedHeart), C(Rank.Nine, Suit.BlackSpade), C(Rank.Eight, Suit.GreenClub), C(Rank.Seven, Suit.BlueDiamond));

        Assert.Throws<ArgumentException>(() => HandEvaluator.Evaluate(hand));
    }
}

public class HandRankTests
{
    [Fact]
    public void HigherCategoryWinsRegardlessOfTiebreakers()
    {
        Assert.True(new HandRank(HandCategory.Flush, [2, 3, 4, 5, 7]) > new HandRank(HandCategory.Straight, [14]));
    }

    [Fact]
    public void SameCategory_HigherFirstTiebreakerWins()
    {
        Assert.True(new HandRank(HandCategory.OnePair, [14, 13, 12]) > new HandRank(HandCategory.OnePair, [14, 10, 9]));
    }

    [Fact]
    public void SameCategory_SecondTiebreakerDecidesWhenFirstIsEqual()
    {
        Assert.True(new HandRank(HandCategory.TwoPair, [14, 13, 12]) > new HandRank(HandCategory.TwoPair, [14, 13, 2]));
    }

    [Fact]
    public void IdenticalCategoryAndTiebreakersReturnZero()
    {
        Assert.Equal(0, new HandRank(HandCategory.TwoPair, [14, 13, 12]).CompareTo(new HandRank(HandCategory.TwoPair, [14, 13, 12])));
    }

    [Fact]
    public void ComparisonIsAntisymmetric()
    {
        var strong = new HandRank(HandCategory.FourOfAKind, [14, 2]);
        var weak = new HandRank(HandCategory.FullHouse, [13, 12]);

        Assert.True(strong > weak);
        Assert.True(weak < strong);
    }

    [Fact]
    public void RoyalFlushBeatsStraightFlush()
    {
        Assert.True(new HandRank(HandCategory.RoyalFlush, [14]) > new HandRank(HandCategory.StraightFlush, [13]));
    }
}
