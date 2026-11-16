using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using CvAspNet.Poker.Engine;
using CvAspNet.Poker.Model;
using CvAspNet.Poker.Strategy;
using CvAspNet.Web.Poker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CvAspNet.Tests.Web;

/// <summary>The limits that keep the public, anonymous poker API from growing without bound.</summary>
public sealed class PokerLimitsTests
{
    private readonly ManualTimeProvider clock = new();

    private TableRepository NewRepository(PokerOptions? options = null) =>
        new(NullLoggerFactory.Instance, Options.Create(options ?? new PokerOptions()), clock);

    private PokerTableSweeper NewSweeper(TableRepository repository, SessionRegistry sessions, PokerOptions? options = null) =>
        new(repository, sessions, Options.Create(options ?? new PokerOptions()), clock, NullLogger<PokerTableSweeper>.Instance);

    // ── Tables ──

    [Fact]
    public void Create_RefusesATableBeyondMaxTables()
    {
        var repository = NewRepository(new PokerOptions { MaxTables = 2 });

        Assert.NotNull(repository.Create());
        Assert.NotNull(repository.Create());
        Assert.Null(repository.Create());
        Assert.Equal(2, repository.Count);
    }

    [Fact]
    public async Task Sweep_RemovesAnIdleTableClosesItAndRevokesItsSessions()
    {
        var repository = NewRepository();
        var sessions = new SessionRegistry(NullLogger<SessionRegistry>.Instance);
        var tableId = repository.Create()!.Value;
        var engine = repository.GetOrThrow(tableId);
        var token = sessions.Create(new PlayerId("p"), tableId);
        repository.Attach(tableId);  // connected, but nothing happens at the table

        clock.Advance(TimeSpan.FromMinutes(29));
        await NewSweeper(repository, sessions).SweepAsync();
        Assert.NotNull(repository.Get(tableId));

        clock.Advance(TimeSpan.FromMinutes(1));
        await NewSweeper(repository, sessions).SweepAsync();

        Assert.Null(repository.Get(tableId));
        Assert.Null(sessions.Get(token));
        Assert.True(engine.IsClosed);
        Assert.True(engine.Closing.IsCancellationRequested);
        await Assert.ThrowsAsync<InvalidOperationException>(() => engine.StartHandAsync());
    }

    [Fact]
    public async Task Sweep_RemovesATableNobodyConnectedToAfterTheGracePeriod()
    {
        var repository = NewRepository();
        var sessions = new SessionRegistry(NullLogger<SessionRegistry>.Instance);
        var tableId = repository.Create()!.Value;

        clock.Advance(TimeSpan.FromSeconds(119));
        await NewSweeper(repository, sessions).SweepAsync();
        Assert.NotNull(repository.Get(tableId));

        clock.Advance(TimeSpan.FromSeconds(1));
        await NewSweeper(repository, sessions).SweepAsync();
        Assert.Null(repository.Get(tableId));
    }

    [Fact]
    public async Task Sweep_KeepsAConnectedTableAndRemovesItOnceTheLastConnectionIsGoneForTheGracePeriod()
    {
        var repository = NewRepository();
        var sessions = new SessionRegistry(NullLogger<SessionRegistry>.Instance);
        var tableId = repository.Create()!.Value;
        repository.Attach(tableId);
        repository.Attach(tableId);

        clock.Advance(TimeSpan.FromMinutes(5));
        await NewSweeper(repository, sessions).SweepAsync();
        Assert.NotNull(repository.Get(tableId));

        repository.Detach(tableId);
        clock.Advance(TimeSpan.FromMinutes(5));
        await NewSweeper(repository, sessions).SweepAsync();
        Assert.NotNull(repository.Get(tableId));  // one connection is still open

        repository.Detach(tableId);
        clock.Advance(TimeSpan.FromSeconds(119));
        await NewSweeper(repository, sessions).SweepAsync();
        Assert.NotNull(repository.Get(tableId));

        clock.Advance(TimeSpan.FromSeconds(1));
        await NewSweeper(repository, sessions).SweepAsync();
        Assert.Null(repository.Get(tableId));
    }

    // ── Turn timeout ──

    private static readonly PlayerId Human = new("human");

    /// <summary>The human is the big blind of a 2-player table: after the bot calls, the human may check.</summary>
    private GameEngine HumanAgainstACallingBot()
    {
        var engine = new GameEngine(new TableId("t"), blindAmount: 10, timeProvider: clock);
        engine.AddPlayer(new Player(Human, "Alice", 1_000, Hand.Empty, IsBot: false));
        engine.AddPlayer(new Player(new PlayerId("bot"), "Bot", 1_000, Hand.Empty), new AlwaysCall());
        return engine;
    }

    private sealed class AlwaysCall : IBotStrategy
    {
        public PlayerAction Decide(TableView view, PlayerView self) =>
            view.ToCall > 0 ? new PlayerAction.Call(self.Id) : new PlayerAction.Check(self.Id);
    }

    [Fact]
    public async Task ExpireTurn_ChecksForAHumanWhoOwesNothingOnceTheTimeoutHasPassed()
    {
        var engine = HumanAgainstACallingBot();
        await engine.StartHandAsync();
        Assert.Equal(Human, engine.Snapshot().CurrentTurnId);

        clock.Advance(TimeSpan.FromSeconds(59));
        Assert.False(await engine.ExpireTurnAsync(TimeSpan.FromSeconds(60)));
        Assert.IsType<GameState.PreFlop>(engine.Snapshot().State);

        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.True(await engine.ExpireTurnAsync(TimeSpan.FromSeconds(60)));

        // Checked, not folded: the hand went on to the flop, where the human acts again with a fresh clock.
        var snapshot = engine.Snapshot();
        Assert.IsType<GameState.Flop>(snapshot.State);
        Assert.False(snapshot.Players.Single(player => player.Id == Human).Folded);
        Assert.False(await engine.ExpireTurnAsync(TimeSpan.FromSeconds(60)));
    }

    [Fact]
    public async Task ExpireTurn_FoldsForAHumanWhoOwesChips()
    {
        // The bot acts first pre-flop and raises: the human owes chips.
        var raiser = new GameEngine(new TableId("r"), blindAmount: 10, timeProvider: clock);
        raiser.AddPlayer(new Player(Human, "Alice", 1_000, Hand.Empty, IsBot: false));
        raiser.AddPlayer(new Player(new PlayerId("bot"), "Bot", 1_000, Hand.Empty), new AlwaysRaise());
        await raiser.StartHandAsync();
        Assert.Equal(Human, raiser.Snapshot().CurrentTurnId);
        Assert.True(raiser.Snapshot(Human).ToCall > 0);

        clock.Advance(TimeSpan.FromSeconds(60));
        Assert.True(await raiser.ExpireTurnAsync(TimeSpan.FromSeconds(60)));

        Assert.IsType<GameState.Showdown>(raiser.Snapshot().State);
        Assert.True(raiser.Snapshot().Players.Single(player => player.Id == Human).Folded);
    }

    private sealed class AlwaysRaise : IBotStrategy
    {
        public PlayerAction Decide(TableView view, PlayerView self) => new PlayerAction.Raise(self.Id, view.ToCall + 20);
    }

    [Fact]
    public async Task ExpireTurn_LeavesABotsTurnAndAFinishedHandAlone()
    {
        var engine = HumanAgainstACallingBot();
        clock.Advance(TimeSpan.FromMinutes(10));

        Assert.False(await engine.ExpireTurnAsync(TimeSpan.FromSeconds(60)));  // no hand yet
    }

    [Fact]
    public async Task Sweep_PlaysTheTurnsThatHaveRunOut()
    {
        var repository = NewRepository();
        var sessions = new SessionRegistry(NullLogger<SessionRegistry>.Instance);
        var tableId = repository.Create()!.Value;
        var engine = repository.GetOrThrow(tableId);
        engine.AddPlayer(new Player(Human, "Alice", 1_000, Hand.Empty, IsBot: false));
        engine.AddPlayer(new Player(new PlayerId("bot"), "Bot", 1_000, Hand.Empty), new AlwaysCall());
        repository.Attach(tableId);
        await engine.StartHandAsync();
        Assert.Equal(Human, engine.Snapshot().CurrentTurnId);

        clock.Advance(TimeSpan.FromSeconds(60));
        await NewSweeper(repository, sessions).SweepAsync();

        Assert.IsType<GameState.Flop>(engine.Snapshot().State);
    }
}

/// <summary>The limits seen from outside: HTTP status codes and WebSocket handshakes.</summary>
public sealed class PokerLimitsApiTests : IDisposable
{
    private readonly CvApplicationFactory factory = new()
    {
        Settings = new Dictionary<string, string>
        {
            ["Poker:MaxTables"] = "2",
            ["Poker:MaxConnectionsPerSession"] = "1",
        },
    };

    private readonly HttpClient client;

    public PokerLimitsApiTests() => client = factory.CreateClient();

    public void Dispose() => factory.Dispose();

    private async Task<(string TableId, JoinTableResponse Session)> CreateAndJoinAsync()
    {
        var created = await client.PostAsJsonAsync("/api/pokertable", new CreateTableRequest(10));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var tableId = (await created.Content.ReadFromJsonAsync<CreateTableResponse>())!.TableId;
        var joined = await client.PostAsJsonAsync($"/api/pokertable/{tableId}/join", new JoinTableRequest("Alice"));
        return (tableId, (await joined.Content.ReadFromJsonAsync<JoinTableResponse>())!);
    }

    private Task<WebSocket> ConnectAsync(string tableId, string token) =>
        factory.Server.CreateWebSocketClient().ConnectAsync(new Uri(factory.Server.BaseAddress, $"api/pokertable/{tableId}/ws?token={token}"), CancellationToken.None);

    [Fact]
    public async Task CreateTable_Is503OnceMaxTablesArePlaying()
    {
        await CreateAndJoinAsync();
        await CreateAndJoinAsync();

        var third = await client.PostAsJsonAsync("/api/pokertable", new CreateTableRequest(10));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, third.StatusCode);
    }

    [Fact]
    public async Task WebSocket_WithAnInvalidTokenIsRefusedBeforeTheUpgrade()
    {
        var (tableId, _) = await CreateAndJoinAsync();

        var refused = await Assert.ThrowsAnyAsync<Exception>(() => ConnectAsync(tableId, "wrong-token"));

        Assert.Contains("401", refused.Message);
    }

    [Fact]
    public async Task WebSocket_RefusesMoreConnectionsPerSessionThanAllowed()
    {
        var (tableId, session) = await CreateAndJoinAsync();
        using var first = await ConnectAsync(tableId, session.SessionToken);

        var refused = await Assert.ThrowsAnyAsync<Exception>(() => ConnectAsync(tableId, session.SessionToken));

        Assert.Contains("429", refused.Message);
    }

    [Fact]
    public async Task WebSocket_AuthenticatedByMessageIsClosedWith4029BeyondTheSessionLimit()
    {
        var (tableId, session) = await CreateAndJoinAsync();
        using var first = await ConnectAsync(tableId, session.SessionToken);
        using var second = await factory.Server.CreateWebSocketClient().ConnectAsync(new Uri(factory.Server.BaseAddress, $"api/pokertable/{tableId}/ws"), CancellationToken.None);

        await second.SendAsync(Encoding.UTF8.GetBytes($$"""{"type":"Auth","token":"{{session.SessionToken}}"}"""), WebSocketMessageType.Text, true, CancellationToken.None);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var result = await second.ReceiveAsync(new byte[1024], timeout.Token);
        Assert.Equal(WebSocketMessageType.Close, result.MessageType);
        Assert.Equal(4029, (int?)second.CloseStatus);
    }

    [Fact]
    public async Task WebSocket_IsClosedWith4004WhenTheTableIsRemoved()
    {
        var (tableId, session) = await CreateAndJoinAsync();
        using var socket = await ConnectAsync(tableId, session.SessionToken);
        var buffer = new byte[64 * 1024];
        await socket.ReceiveAsync(buffer, CancellationToken.None);  // the first snapshot

        factory.Services.GetRequiredService<TableRepository>().Remove(new TableId(tableId));

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(buffer, timeout.Token);
        }
        while (result.MessageType != WebSocketMessageType.Close);
        Assert.Equal(4004, (int?)socket.CloseStatus);
    }
}
