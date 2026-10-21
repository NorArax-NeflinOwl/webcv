using CvAspNet.Poker.Engine;
using CvAspNet.Poker.Evaluation;
using CvAspNet.Poker.Model;
using CvAspNet.Poker.Strategy;
using CvAspNet.Web.Poker;
using Microsoft.Extensions.Logging.Abstractions;

namespace CvAspNet.Tests.Poker;

/// <summary>
/// A full Texas Hold'em hand with 1 human and 2 bots.
/// <para>
/// Seats [human, bot1, bot2], dealer 0 → SB = bot1, BB = bot2, UTG = human.
/// Deck: human A♥ K♥, bot1 2♣ 3♣, bot2 4♦ 5♦, flop Q♥ J♥ T♥, turn 9♣, river 8♣ —
/// the human holds a royal flush, both bots the same straight.
/// Pot: human calls 10, bot1 completes to 10, bot2's big blind 10 → 30; nobody bets later.
/// Human 1000 - 10 + 30 = 1020, each bot 990, 3000 in total.
/// </para>
/// </summary>
public class PokerGameIntegrationTests
{
    /// <summary>Never folds: calls when chips are owed, otherwise checks, so the hand always reaches the showdown.</summary>
    private sealed class AlwaysCallStrategy : IBotStrategy
    {
        public PlayerAction Decide(TableView view, PlayerView player) =>
            view.ToCall == 0 ? new PlayerAction.Check(player.Id) : new PlayerAction.Call(player.Id);
    }

    private static readonly PlayerId HumanId = new("human");
    private static readonly PlayerId Bot1Id = new("bot1");
    private static readonly PlayerId Bot2Id = new("bot2");

    private static Card C(Rank rank, Suit suit) => new(rank, suit);

    private static Deck RoyalFlushDeck() => Deck.Of(
        C(Rank.Ace, Suit.RedHeart), C(Rank.King, Suit.RedHeart),           // human
        C(Rank.Two, Suit.GreenClub), C(Rank.Three, Suit.GreenClub),        // bot1
        C(Rank.Four, Suit.BlueDiamond), C(Rank.Five, Suit.BlueDiamond),    // bot2
        C(Rank.Queen, Suit.RedHeart), C(Rank.Jack, Suit.RedHeart), C(Rank.Ten, Suit.RedHeart),  // flop
        C(Rank.Nine, Suit.GreenClub),                                       // turn
        C(Rank.Eight, Suit.GreenClub));                                     // river

    private static GameEngine BuildEngine()
    {
        var engine = new GameEngine(new TableId("integration-test"), blindAmount: 10, deckFactory: RoyalFlushDeck);
        engine.AddPlayer(new Player(HumanId, "Alice", 1_000, Hand.Empty, IsBot: false));
        engine.AddPlayer(new Player(Bot1Id, "Bot1", 1_000, Hand.Empty), new AlwaysCallStrategy());
        engine.AddPlayer(new Player(Bot2Id, "Bot2", 1_000, Hand.Empty), new AlwaysCallStrategy());
        return engine;
    }

    /// <summary>Pre-flop the human calls the big blind; on the flop, turn and river the bots check first and the human last.</summary>
    private static async Task<GameEngine> PlayFullHandAsync()
    {
        var engine = BuildEngine();
        await engine.StartHandAsync();
        await engine.ApplyActionAsync(new PlayerAction.Call(HumanId));
        for (var street = 0; street < 3; street++)
        {
            await engine.ApplyActionAsync(new PlayerAction.Check(HumanId));
        }
        return engine;
    }

    private static GameState.Showdown ShowdownOf(GameEngine engine) => Assert.IsType<GameState.Showdown>(engine.Snapshot().State);

    // ── Phase progression ──

    [Fact]
    public async Task AfterStartHand_StateIsPreFlopAndHumanIsUtg()
    {
        var engine = BuildEngine();
        await engine.StartHandAsync();

        var snapshot = engine.Snapshot();
        Assert.IsType<GameState.PreFlop>(snapshot.State);
        Assert.Equal(HumanId, snapshot.CurrentTurnId);
    }

    [Fact]
    public async Task HumanCall_AdvancesToFlopWithThreeBoardCards()
    {
        var engine = BuildEngine();
        await engine.StartHandAsync();
        await engine.ApplyActionAsync(new PlayerAction.Call(HumanId));  // bots play → Flop

        var snapshot = engine.Snapshot();
        Assert.IsType<GameState.Flop>(snapshot.State);
        Assert.Equal(3, snapshot.Board.Count);
        Assert.Equal(HumanId, snapshot.CurrentTurnId);  // the human acts last after the flop
    }

    [Fact]
    public async Task HumanCheckOnFlop_AdvancesToTurnWithFourBoardCards()
    {
        var engine = BuildEngine();
        await engine.StartHandAsync();
        await engine.ApplyActionAsync(new PlayerAction.Call(HumanId));
        await engine.ApplyActionAsync(new PlayerAction.Check(HumanId));

        var snapshot = engine.Snapshot();
        Assert.IsType<GameState.Turn>(snapshot.State);
        Assert.Equal(4, snapshot.Board.Count);
    }

    [Fact]
    public async Task HumanCheckOnTurn_AdvancesToRiverWithFiveBoardCards()
    {
        var engine = BuildEngine();
        await engine.StartHandAsync();
        await engine.ApplyActionAsync(new PlayerAction.Call(HumanId));
        await engine.ApplyActionAsync(new PlayerAction.Check(HumanId));
        await engine.ApplyActionAsync(new PlayerAction.Check(HumanId));

        var snapshot = engine.Snapshot();
        Assert.IsType<GameState.River>(snapshot.State);
        Assert.Equal(5, snapshot.Board.Count);
    }

    [Fact]
    public async Task FullHand_ReachesShowdownAfterRiverCheck()
    {
        var engine = await PlayFullHandAsync();

        Assert.IsType<GameState.Showdown>(engine.Snapshot().State);
    }

    // ── Card visibility ──

    [Fact]
    public async Task OpponentHoleCards_AreHiddenFromHumanBeforeShowdown()
    {
        var engine = BuildEngine();
        await engine.StartHandAsync();

        var players = engine.Snapshot(forPlayer: HumanId).Players;

        Assert.NotNull(players.Single(player => player.Id == HumanId).HoleCards);
        Assert.Null(players.Single(player => player.Id == Bot1Id).HoleCards);
        Assert.Null(players.Single(player => player.Id == Bot2Id).HoleCards);
    }

    [Fact]
    public async Task AllHoleCards_AreRevealedAtShowdown()
    {
        var engine = await PlayFullHandAsync();

        Assert.All(engine.Snapshot(forPlayer: HumanId).Players, player => Assert.Equal(2, player.HoleCards?.Count));
    }

    [Fact]
    public async Task HumanHoleCards_AreAceAndKingOfHearts()
    {
        var engine = BuildEngine();
        await engine.StartHandAsync();

        var cards = engine.Snapshot(forPlayer: HumanId).Players.Single(player => player.Id == HumanId).HoleCards!;

        Assert.Equal([Rank.Ace, Rank.King], cards.Select(card => card.Rank));
        Assert.All(cards, card => Assert.Equal(Suit.RedHeart, card.Suit));
    }

    // ── Chip accounting ──

    [Fact]
    public async Task TotalChips_AreConservedAcrossTheFullHand()
    {
        var engine = await PlayFullHandAsync();

        Assert.Equal(3_000, engine.Snapshot().Players.Sum(player => player.Coins));
    }

    [Fact]
    public async Task Human_WinsThePot()
    {
        var engine = await PlayFullHandAsync();

        Assert.Equal(1_020, engine.Snapshot().Players.Single(player => player.Id == HumanId).Coins);
    }

    [Fact]
    public async Task LosingBots_EachSpendOnlyTheirBlindOrCall()
    {
        var players = (await PlayFullHandAsync()).Snapshot().Players;

        Assert.Equal(990, players.Single(player => player.Id == Bot1Id).Coins);
        Assert.Equal(990, players.Single(player => player.Id == Bot2Id).Coins);
    }

    // ── Showdown ranking ──

    [Fact]
    public async Task ShowdownRanking_HasAllThreePlayers()
    {
        Assert.Equal(3, ShowdownOf(await PlayFullHandAsync()).Ranking.Count);
    }

    [Fact]
    public async Task Human_RanksFirstWithRoyalFlush()
    {
        var top = ShowdownOf(await PlayFullHandAsync()).Ranking[0];

        Assert.Equal(HumanId, top.PlayerId);
        Assert.Equal(HandCategory.RoyalFlush, HandEvaluator.Evaluate(top.HandValue).Category);
    }

    [Fact]
    public async Task Bots_TieForSecondPlaceWithEqualStraights()
    {
        var ranking = ShowdownOf(await PlayFullHandAsync()).Ranking;

        Assert.Equal(new HashSet<PlayerId> { Bot1Id, Bot2Id }, ranking.Skip(1).Select(result => result.PlayerId).ToHashSet());
        Assert.Equal(0, ranking.Single(result => result.PlayerId == Bot1Id).CompareTo(ranking.Single(result => result.PlayerId == Bot2Id)));
    }

    // ── To call ──

    [Fact]
    public async Task ToCall_ForUtgIsTheBigBlindAtStartOfHand()
    {
        var engine = BuildEngine();
        await engine.StartHandAsync();

        Assert.Equal(10, engine.Snapshot(forPlayer: HumanId).ToCall);
    }

    [Fact]
    public async Task ToCall_IsZeroOnTheFlopAfterEqualization()
    {
        var engine = BuildEngine();
        await engine.StartHandAsync();
        await engine.ApplyActionAsync(new PlayerAction.Call(HumanId));

        Assert.Equal(0, engine.Snapshot(forPlayer: HumanId).ToCall);
    }

    // ── Events ──

    /// <summary>
    /// 13 state changes in a full hand: start 1 (PreFlop); human call 5 (call, bot1 call, bot2 check → Flop,
    /// bot1 check, bot2 check); flop check 3 (→ Turn, 2 bot checks); turn check 3 (→ River, 2 bot checks);
    /// river check 1 (→ Showdown).
    /// </summary>
    [Fact]
    public async Task Exactly13StateChangedEvents_AreRaisedAcrossAFullHand()
    {
        var engine = BuildEngine();
        var count = 0;
        engine.EventPublished += gameEvent =>
        {
            if (gameEvent is GameEvent.StateChanged) Interlocked.Increment(ref count);
        };

        await engine.StartHandAsync();
        await engine.ApplyActionAsync(new PlayerAction.Call(HumanId));
        for (var street = 0; street < 3; street++)
        {
            await engine.ApplyActionAsync(new PlayerAction.Check(HumanId));
        }

        Assert.Equal(13, count);
    }

    [Fact]
    public async Task EventListener_MayReadTheSnapshot()
    {
        var engine = BuildEngine();
        var phases = new List<string>();
        engine.EventPublished += _ => phases.Add(engine.Snapshot().State.GetType().Name);

        await engine.StartHandAsync();

        Assert.Equal(["PreFlop"], phases);
    }

    // ── TableRepository + SessionRegistry ──

    [Fact]
    public void TableRepository_StoresAndRetrievesAGameEngine()
    {
        var repository = new TableRepository(NullLoggerFactory.Instance);
        var tableId = repository.Create(blindAmount: 10);

        Assert.NotNull(repository.Get(tableId));
        Assert.Equal(1, repository.Count);
    }

    [Fact]
    public void SessionRegistry_IssuesATokenValidForItsTable()
    {
        var tableId = new TableRepository(NullLoggerFactory.Instance).Create();
        var registry = new SessionRegistry(NullLogger<SessionRegistry>.Instance);
        var playerId = new PlayerId("player-1");

        var session = registry.Validate(registry.Create(playerId, tableId), tableId);

        Assert.NotNull(session);
        Assert.Equal(playerId, session.PlayerId);
        Assert.Equal(tableId, session.TableId);
    }

    [Fact]
    public void SessionRegistry_RejectsATokenUsedAgainstAnotherTable()
    {
        var repository = new TableRepository(NullLoggerFactory.Instance);
        var registry = new SessionRegistry(NullLogger<SessionRegistry>.Instance);
        var token = registry.Create(new PlayerId("p"), repository.Create());

        Assert.Null(registry.Validate(token, repository.Create()));
    }

    [Fact]
    public void SessionRegistry_RevokeRemovesTheSession()
    {
        var registry = new SessionRegistry(NullLogger<SessionRegistry>.Instance);
        var token = registry.Create(new PlayerId("p"), new TableRepository(NullLoggerFactory.Instance).Create());

        registry.Revoke(token);

        Assert.Null(registry.Get(token));
    }
}
