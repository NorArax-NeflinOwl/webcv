using CvAspNet.Poker.Engine;
using CvAspNet.Poker.Model;

namespace CvAspNet.Tests.Poker;

public class GameEngineTests
{
    private static readonly PlayerId P0 = new("p0");
    private static readonly PlayerId P1 = new("p1");

    private static Player NewPlayer(PlayerId id, string name, int coins = 1000) => new(id, name, coins, Hand.Empty);

    private static Card C(Rank rank, Suit suit) => new(rank, suit);

    /// <summary>
    /// A fixed deck for 2 players: p0's two cards, p1's two cards, the flop, the turn, the river.
    /// </summary>
    private static Deck Deck2(Card p0First, Card p0Second, Card p1First, Card p1Second, Card flop1, Card flop2, Card flop3, Card turn, Card river) =>
        Deck.Of(p0First, p0Second, p1First, p1Second, flop1, flop2, flop3, turn, river);

    private static GameEngine TwoPlayerEngine(Func<Deck>? deckFactory = null)
    {
        var engine = new GameEngine(new TableId("test"), blindAmount: 10, deckFactory: deckFactory);
        engine.AddPlayer(NewPlayer(P0, "Alice"));
        engine.AddPlayer(NewPlayer(P1, "Bob"));
        return engine;
    }

    /// <summary>Board A-A-A-A-K: four aces with a king for both players, whatever their hole cards.</summary>
    private static Deck TieDeck() => Deck2(
        C(Rank.Two, Suit.GreenClub), C(Rank.Three, Suit.BlackSpade),
        C(Rank.Four, Suit.BlueDiamond), C(Rank.Five, Suit.RedHeart),
        C(Rank.Ace, Suit.RedHeart), C(Rank.Ace, Suit.BlackSpade), C(Rank.Ace, Suit.GreenClub),
        C(Rank.Ace, Suit.BlueDiamond),
        C(Rank.King, Suit.RedHeart));

    /// <summary>Pre-flop the small blind calls and the big blind checks; then both check to the showdown.</summary>
    private static async Task CallThenCheckDownAsync(GameEngine engine)
    {
        await engine.ApplyActionAsync(new PlayerAction.Call(engine.Snapshot().CurrentTurnId!.Value));
        await engine.ApplyActionAsync(new PlayerAction.Check(engine.Snapshot().CurrentTurnId!.Value));
        for (var i = 0; i < 6; i++)
        {
            if (engine.Snapshot().CurrentTurnId is { } turn) await engine.ApplyActionAsync(new PlayerAction.Check(turn));
        }
    }

    // ── StartHand ──

    [Fact]
    public async Task StartHand_RequiresAtLeastTwoPlayers()
    {
        var engine = new GameEngine(new TableId("test"));
        engine.AddPlayer(NewPlayer(P0, "Alice"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => engine.StartHandAsync());
    }

    [Fact]
    public async Task StartHand_SetsPreFlopState()
    {
        var engine = TwoPlayerEngine();
        await engine.StartHandAsync();

        Assert.IsType<GameState.PreFlop>(engine.Snapshot().State);
    }

    [Fact]
    public async Task StartHand_IsRejectedWhileAHandIsInProgress()
    {
        var engine = TwoPlayerEngine();
        await engine.StartHandAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => engine.StartHandAsync());
    }

    [Fact]
    public async Task Snapshot_ReturnsCurrentTurnIdAfterStartHand()
    {
        var engine = TwoPlayerEngine();
        await engine.StartHandAsync();

        Assert.NotNull(engine.Snapshot().CurrentTurnId);
    }

    [Fact]
    public async Task Snapshot_HidesOpponentHoleCardsBeforeShowdown()
    {
        var engine = TwoPlayerEngine();
        await engine.StartHandAsync();

        var snapshot = engine.Snapshot(forPlayer: P0);

        Assert.NotNull(snapshot.Players.Single(player => player.Id == P0).HoleCards);  // own cards visible
        Assert.Null(snapshot.Players.Single(player => player.Id == P1).HoleCards);     // opponent's hidden
    }

    [Fact]
    public void AddPlayer_RejectsTheSamePlayerTwice()
    {
        var engine = TwoPlayerEngine();

        Assert.Throws<ArgumentException>(() => engine.AddPlayer(NewPlayer(P0, "Alice again")));
    }

    // ── Action validation ──

    [Fact]
    public async Task ActionOutOfTurn_Throws()
    {
        var engine = TwoPlayerEngine();
        await engine.StartHandAsync();

        var firstToAct = engine.Snapshot().CurrentTurnId!.Value;
        var notMyTurn = firstToAct == P0 ? P1 : P0;

        await Assert.ThrowsAsync<InvalidOperationException>(() => engine.ApplyActionAsync(new PlayerAction.Check(notMyTurn)));
    }

    [Fact]
    public async Task Check_IsIllegalWhenPlayerOwesChips()
    {
        var engine = TwoPlayerEngine();
        await engine.StartHandAsync();

        // The small blind posted 5 of the current bet of 10: they must call or fold.
        var firstToAct = engine.Snapshot().CurrentTurnId!.Value;
        Assert.True(engine.Snapshot(firstToAct).ToCall > 0);

        await Assert.ThrowsAsync<InvalidOperationException>(() => engine.ApplyActionAsync(new PlayerAction.Check(firstToAct)));
    }

    [Fact]
    public async Task Action_IsRejectedWhenNoHandIsInProgress()
    {
        var engine = TwoPlayerEngine();

        await Assert.ThrowsAsync<InvalidOperationException>(() => engine.ApplyActionAsync(new PlayerAction.Check(P0)));
    }

    // ── Fold ──

    [Fact]
    public async Task Fold_PotGoesToTheOnlyRemainingPlayer()
    {
        var engine = TwoPlayerEngine(() => Deck2(
            C(Rank.Two, Suit.GreenClub), C(Rank.Three, Suit.GreenClub),
            C(Rank.Four, Suit.BlueDiamond), C(Rank.Five, Suit.BlueDiamond),
            C(Rank.Six, Suit.RedHeart), C(Rank.Seven, Suit.RedHeart), C(Rank.Eight, Suit.RedHeart),
            C(Rank.Nine, Suit.RedHeart),
            C(Rank.Ten, Suit.RedHeart)));
        await engine.StartHandAsync();

        var firstToAct = engine.Snapshot().CurrentTurnId!.Value;
        await engine.ApplyActionAsync(new PlayerAction.Fold(firstToAct));

        var final = engine.Snapshot();
        Assert.IsType<GameState.Showdown>(final.State);
        Assert.True(final.Players.Single(player => player.Id != firstToAct).Coins > 1000, "Winner should have more than the starting 1000");
        Assert.True(final.Players.Single(player => player.Id == firstToAct).Coins < 1000, "Folder should have less than the starting 1000");
    }

    [Fact]
    public async Task Fold_TotalChipsAreConserved()
    {
        var engine = TwoPlayerEngine();
        await engine.StartHandAsync();

        await engine.ApplyActionAsync(new PlayerAction.Fold(engine.Snapshot().CurrentTurnId!.Value));

        Assert.Equal(2000, engine.Snapshot().Players.Sum(player => player.Coins));
    }

    // ── Full hand to showdown ──

    [Fact]
    public async Task FullHand_ShowdownReachedAfterCallAndChecks_ChipsConserved()
    {
        var engine = TwoPlayerEngine(() => Deck2(
            C(Rank.Ace, Suit.RedHeart), C(Rank.King, Suit.RedHeart),
            C(Rank.Two, Suit.GreenClub), C(Rank.Three, Suit.GreenClub),
            C(Rank.Four, Suit.BlueDiamond), C(Rank.Five, Suit.BlueDiamond), C(Rank.Seven, Suit.BlackSpade),
            C(Rank.Eight, Suit.BlackSpade),
            C(Rank.Nine, Suit.BlackSpade)));
        await engine.StartHandAsync();

        await CallThenCheckDownAsync(engine);

        var final = engine.Snapshot();
        Assert.IsType<GameState.Showdown>(final.State);
        Assert.Equal(2000, final.Players.Sum(player => player.Coins));
    }

    [Fact]
    public async Task NextHand_CanStartAfterShowdown()
    {
        var engine = TwoPlayerEngine();
        await engine.StartHandAsync();
        await engine.ApplyActionAsync(new PlayerAction.Fold(engine.Snapshot().CurrentTurnId!.Value));

        await engine.StartHandAsync();

        Assert.IsType<GameState.PreFlop>(engine.Snapshot().State);
    }

    // ── Tie (split pot) ──

    [Fact]
    public async Task Tie_PotSplitEvenly_BothPlayersReturnToStartingChips()
    {
        var engine = TwoPlayerEngine(TieDeck);
        await engine.StartHandAsync();

        await CallThenCheckDownAsync(engine);

        var final = engine.Snapshot();
        Assert.IsType<GameState.Showdown>(final.State);
        // pot = 20, split → 10 each; both invested 10 → back to 1000.
        Assert.Equal(1000, final.Players.Single(player => player.Id == P0).Coins);
        Assert.Equal(1000, final.Players.Single(player => player.Id == P1).Coins);
    }

    [Fact]
    public async Task Tie_TotalChipsAreConserved()
    {
        var engine = TwoPlayerEngine(TieDeck);
        await engine.StartHandAsync();

        await CallThenCheckDownAsync(engine);

        Assert.Equal(2000, engine.Snapshot().Players.Sum(player => player.Coins));
    }

    // ── Raise ──

    [Fact]
    public async Task Raise_OpponentMustRespond()
    {
        var engine = TwoPlayerEngine();
        await engine.StartHandAsync();

        var smallBlind = engine.Snapshot().CurrentTurnId!.Value;
        await engine.ApplyActionAsync(new PlayerAction.Raise(smallBlind, Amount: 50));

        var nextTurn = engine.Snapshot().CurrentTurnId;
        Assert.NotNull(nextTurn);
        Assert.NotEqual(smallBlind, nextTurn);
    }

    [Fact]
    public async Task Raise_CheckAfterRaiseIsIllegal()
    {
        var engine = TwoPlayerEngine();
        await engine.StartHandAsync();

        await engine.ApplyActionAsync(new PlayerAction.Raise(engine.Snapshot().CurrentTurnId!.Value, Amount: 50));

        // The big blind must call or fold, not check.
        var bigBlind = engine.Snapshot().CurrentTurnId!.Value;
        await Assert.ThrowsAsync<InvalidOperationException>(() => engine.ApplyActionAsync(new PlayerAction.Check(bigBlind)));
    }

    [Fact]
    public async Task Raise_AboveTheChipStackIsRejected()
    {
        var engine = TwoPlayerEngine();
        await engine.StartHandAsync();

        var smallBlind = engine.Snapshot().CurrentTurnId!.Value;
        await Assert.ThrowsAsync<ArgumentException>(() => engine.ApplyActionAsync(new PlayerAction.Raise(smallBlind, Amount: 5000)));
    }
}
