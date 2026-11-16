using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using CvAspNet.Web.Poker;

namespace CvAspNet.Tests.Web;

/// <summary>The protocol pokerGame.js speaks: REST to create, join and start; WebSocket for snapshots and actions.</summary>
public sealed class PokerApiTests : IDisposable
{
    private static readonly TimeSpan ReceiveTimeout = TimeSpan.FromSeconds(10);

    private readonly CvApplicationFactory factory = new();
    private readonly HttpClient client;

    public PokerApiTests() => client = factory.CreateClient();

    public void Dispose() => factory.Dispose();

    private async Task<string> CreateTableAsync()
    {
        var created = await client.PostAsJsonAsync("/api/pokertable", new CreateTableRequest(BlindAmount: 10));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        return (await created.Content.ReadFromJsonAsync<CreateTableResponse>())!.TableId;
    }

    private async Task<(string TableId, JoinTableResponse Session)> CreateAndJoinAsync()
    {
        var tableId = await CreateTableAsync();

        var joined = await client.PostAsJsonAsync($"/api/pokertable/{tableId}/join", new JoinTableRequest("Alice"));
        Assert.Equal(HttpStatusCode.Created, joined.StatusCode);
        return (tableId, (await joined.Content.ReadFromJsonAsync<JoinTableResponse>())!);
    }

    /// <summary>The page's way: the token in the Authorization header, never in the URL.</summary>
    private Task<HttpResponseMessage> StartAsync(string tableId, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/pokertable/{tableId}/start");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client.SendAsync(request);
    }

    private async Task<TableSnapshotDto?> SnapshotAsync(string tableId, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/pokertable/{tableId}/snapshot");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TableSnapshotDto>();
    }

    /// <summary>The page's way: a plain URL, then the token as the first message.</summary>
    private async Task<WebSocket> ConnectAsync(string tableId, string token)
    {
        var socket = await factory.Server.CreateWebSocketClient().ConnectAsync(new Uri(factory.Server.BaseAddress, $"api/pokertable/{tableId}/ws"), CancellationToken.None);
        await SendTextAsync(socket, JsonSerializer.Serialize(new { type = "Auth", token }));
        return socket;
    }

    private static Task SendTextAsync(WebSocket socket, string text) =>
        socket.SendAsync(Encoding.UTF8.GetBytes(text), WebSocketMessageType.Text, endOfMessage: true, CancellationToken.None);

    private static async Task<JsonElement?> ReceiveAsync(WebSocket socket)
    {
        using var timeout = new CancellationTokenSource(ReceiveTimeout);
        var buffer = new byte[64 * 1024];
        var length = 0;
        while (true)
        {
            var result = await socket.ReceiveAsync(buffer.AsMemory(length), timeout.Token);
            if (result.MessageType == WebSocketMessageType.Close) return null;
            length += result.Count;
            if (result.EndOfMessage) return JsonDocument.Parse(Encoding.UTF8.GetString(buffer, 0, length)).RootElement;
        }
    }

    /// <summary>Skips snapshots until a message matches; snapshots may arrive at any time.</summary>
    private static async Task<JsonElement> ReceiveUntilAsync(WebSocket socket, Func<JsonElement, bool> match)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var message = await ReceiveAsync(socket) ?? throw new InvalidOperationException("The server closed the connection");
            if (match(message)) return message;
        }
        throw new InvalidOperationException("No matching message");
    }

    [Fact]
    public async Task CreateTable_SeatsTwoBots()
    {
        var (tableId, session) = await CreateAndJoinAsync();

        Assert.Equal(HttpStatusCode.NoContent, (await StartAsync(tableId, session.SessionToken)).StatusCode);
        var snapshot = await SnapshotAsync(tableId, session.SessionToken);
        Assert.Equal(3, snapshot!.Players.Count);
        Assert.Equal(2, snapshot.Players.Single(player => player.Id == session.PlayerId).HoleCards?.Count);
    }

    [Fact]
    public async Task CreateTable_AcceptsAnEmptyBody()
    {
        var response = await client.PostAsync("/api/pokertable", null);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Tables_AreNotListed()
    {
        await CreateAndJoinAsync();

        var response = await client.GetAsync("/api/pokertable");

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    [Fact]
    public async Task Join_ASecondHumanIs409()
    {
        var (tableId, _) = await CreateAndJoinAsync();

        var stranger = await client.PostAsJsonAsync($"/api/pokertable/{tableId}/join", new JoinTableRequest("Mallory"));

        Assert.Equal(HttpStatusCode.Conflict, stranger.StatusCode);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(-10)]
    [InlineData(PokerInput.MaxBlind + 1)]
    public async Task CreateTable_RejectsABlindOutOfRange(int blindAmount)
    {
        var response = await client.PostAsJsonAsync("/api/pokertable", new CreateTableRequest(blindAmount));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    public async Task Join_RejectsCoinsOutOfRange(int coins)
    {
        var tableId = await CreateTableAsync();

        var response = await client.PostAsJsonAsync($"/api/pokertable/{tableId}/join", new JoinTableRequest("Alice", coins));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("A name that is much too long for a seat")]
    [InlineData("Line\nbreak")]
    [InlineData("Evil\u202Eeman")]
    public async Task Join_RejectsAnInvalidName(string playerName)
    {
        var tableId = await CreateTableAsync();

        var response = await client.PostAsJsonAsync($"/api/pokertable/{tableId}/join", new JoinTableRequest(playerName));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Join_TrimsTheName()
    {
        var tableId = await CreateTableAsync();
        var joined = await (await client.PostAsJsonAsync($"/api/pokertable/{tableId}/join", new JoinTableRequest("  <b>Alice</b>  "))).Content.ReadFromJsonAsync<JoinTableResponse>();
        await StartAsync(tableId, joined!.SessionToken);

        var snapshot = await SnapshotAsync(tableId, joined.SessionToken);

        // Markup is kept as text: the page renders names with textContent.
        Assert.Equal("<b>Alice</b>", snapshot!.Players.Single(player => player.Id == joined.PlayerId).Name);
    }

    [Fact]
    public async Task Join_UnknownTableIs404()
    {
        var response = await client.PostAsJsonAsync("/api/pokertable/missing/join", new JoinTableRequest("Alice"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Start_WithAnInvalidTokenIs401()
    {
        var (tableId, _) = await CreateAndJoinAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, (await StartAsync(tableId, "wrong")).StatusCode);
    }

    [Fact]
    public async Task Start_WhileAHandIsInProgressIs409()
    {
        var (tableId, session) = await CreateAndJoinAsync();
        await StartAsync(tableId, session.SessionToken);

        var second = await StartAsync(tableId, session.SessionToken);

        // 409 — unless both bots folded and the hand is already over, which starts the next one (204).
        Assert.Contains(second.StatusCode, new[] { HttpStatusCode.Conflict, HttpStatusCode.NoContent });
    }

    [Fact]
    public async Task WebSocket_PushesSnapshotsAndAnswersBadActionsWithAnError()
    {
        var (tableId, session) = await CreateAndJoinAsync();
        using var socket = await ConnectAsync(tableId, session.SessionToken);

        var initial = (await ReceiveAsync(socket))!.Value;
        Assert.Equal("WaitingForPlayers", initial.GetProperty("phase").GetString());

        await StartAsync(tableId, session.SessionToken);
        var started = await ReceiveUntilAsync(socket, message => message.TryGetProperty("phase", out var phase) && phase.GetString() != "WaitingForPlayers");
        Assert.Equal(3, started.GetProperty("players").GetArrayLength());

        await socket.SendAsync("not json"u8.ToArray(), WebSocketMessageType.Text, endOfMessage: true, CancellationToken.None);
        var error = await ReceiveUntilAsync(socket, message => message.TryGetProperty("error", out _));
        Assert.Equal("Invalid action format", error.GetProperty("error").GetString());

        await socket.SendAsync("""{"type":"Raise"}"""u8.ToArray(), WebSocketMessageType.Text, endOfMessage: true, CancellationToken.None);
        var missingAmount = await ReceiveUntilAsync(socket, message => message.TryGetProperty("error", out _));
        Assert.Equal("Invalid action format", missingAmount.GetProperty("error").GetString());

        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
    }

    [Fact]
    public async Task WebSocket_AppliesTheHumanAction()
    {
        var (tableId, session) = await CreateAndJoinAsync();
        using var socket = await ConnectAsync(tableId, session.SessionToken);
        await ReceiveAsync(socket);
        await StartAsync(tableId, session.SessionToken);

        // The human is the big blind: once it is their turn, folding ends their part in the hand.
        var myTurn = await ReceiveUntilAsync(socket, message =>
            message.TryGetProperty("phase", out var phase) &&
            (phase.GetString() == "Showdown" || message.GetProperty("currentTurnId").GetString() == session.PlayerId));
        if (myTurn.GetProperty("phase").GetString() == "Showdown") return;  // both bots folded first

        await socket.SendAsync("""{"type":"Fold"}"""u8.ToArray(), WebSocketMessageType.Text, endOfMessage: true, CancellationToken.None);
        var afterFold = await ReceiveUntilAsync(socket, message =>
            message.TryGetProperty("players", out var players) &&
            players.EnumerateArray().Any(player => player.GetProperty("id").GetString() == session.PlayerId && player.GetProperty("folded").GetBoolean()));

        Assert.NotEqual(session.PlayerId, afterFold.GetProperty("currentTurnId").GetString());
    }

    [Fact]
    public async Task WebSocket_WithAnInvalidAuthMessageIsClosedWith4001()
    {
        var (tableId, _) = await CreateAndJoinAsync();
        using var socket = await ConnectAsync(tableId, "wrong-token");

        Assert.Null(await ReceiveAsync(socket));
        Assert.Equal(4001, (int?)socket.CloseStatus);
    }

    [Fact]
    public async Task WebSocket_WhoseFirstMessageIsNotAuthIsClosedWith4001()
    {
        var (tableId, _) = await CreateAndJoinAsync();
        using var socket = await factory.Server.CreateWebSocketClient().ConnectAsync(new Uri(factory.Server.BaseAddress, $"api/pokertable/{tableId}/ws"), CancellationToken.None);
        await SendTextAsync(socket, """{"type":"Fold"}""");

        Assert.Null(await ReceiveAsync(socket));
        Assert.Equal(4001, (int?)socket.CloseStatus);
    }

    [Fact]
    public async Task OlderClients_MayStillPassTheTokenInTheQuery()
    {
        var (tableId, session) = await CreateAndJoinAsync();
        using var socket = await factory.Server.CreateWebSocketClient().ConnectAsync(
            new Uri(factory.Server.BaseAddress, $"api/pokertable/{tableId}/ws?token={session.SessionToken}"), CancellationToken.None);

        Assert.Equal("WaitingForPlayers", (await ReceiveAsync(socket))!.Value.GetProperty("phase").GetString());
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"/api/pokertable/{tableId}/start?token={session.SessionToken}", null)).StatusCode);
    }

    [Fact]
    public async Task Start_WithoutAnyTokenIs401()
    {
        var (tableId, _) = await CreateAndJoinAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync($"/api/pokertable/{tableId}/start", null)).StatusCode);
    }
}
