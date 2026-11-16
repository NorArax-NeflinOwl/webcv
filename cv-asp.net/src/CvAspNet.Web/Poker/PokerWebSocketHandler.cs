using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using CvAspNet.Poker.Engine;
using CvAspNet.Poker.Model;
using Microsoft.Extensions.Options;

namespace CvAspNet.Web.Poker;

/// <summary>
/// WebSocket endpoint /api/pokertable/{id}/ws.
/// <list type="bullet">
/// <item>Authentication: the token from POST /api/pokertable/{id}/join, which must belong to this table, comes as
/// the first message, {"type":"Auth","token":"…"}, within <see cref="AuthTimeout"/>; otherwise the connection
/// is closed with 4001 (4004 when the table does not exist, 4029 when the session has too many connections).
/// The page sends it that way, so that the token stays out of the URL and out of the gateway's access log.</item>
/// <item>The older form /ws?token=… is still accepted; it is checked before the upgrade, which is refused with
/// 401, 404 or 429 instead.</item>
/// <item>More than <see cref="PokerOptions.MaxConnectionsPerSession"/> connections of one session are refused,
/// more than <see cref="PokerOptions.MaxConnections"/> in total with 503 before the upgrade.</item>
/// <item>Server → client: after every state change, a <see cref="TableSnapshotDto"/> from this player's side
/// (opponents' hole cards hidden until the showdown).</item>
/// <item>Client → server: an <see cref="ActionRequest"/>; a rejected action is answered with
/// {"error": "…"} and the connection stays open, so the player can try again.</item>
/// <item>When the table is removed (idle, or abandoned), the server closes the connection with 4004.</item>
/// </list>
/// </summary>
public sealed class PokerWebSocketHandler(
    TableRepository tableRepository,
    SessionRegistry sessionRegistry,
    IOptions<PokerOptions> options,
    ILogger<PokerWebSocketHandler> log)
{
    private const WebSocketCloseStatus Unauthenticated = (WebSocketCloseStatus)4001;
    private const WebSocketCloseStatus TableMissing = (WebSocketCloseStatus)4004;
    private const WebSocketCloseStatus TooManyConnections = (WebSocketCloseStatus)4029;
    private static readonly TimeSpan AuthTimeout = TimeSpan.FromSeconds(10);
    private const int MaxMessageBytes = 4 * 1024;
    private const int MaxLoggedPayloadChars = 120;
    private static readonly TimeSpan CloseHandshakeTimeout = TimeSpan.FromSeconds(5);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        AllowOutOfOrderMetadataProperties = true,
        RespectRequiredConstructorParameters = true,
    };

    private readonly ConcurrentDictionary<string, int> connectionsPerSession = new();
    private int connectionCount;

    /// <summary>One connection; sends are serialised because a WebSocket allows only one at a time.</summary>
    private sealed class Connection(WebSocket socket, PlayerSession session, GameEngine engine) : IDisposable
    {
        private readonly SemaphoreSlim sendLock = new(1, 1);

        public WebSocket Socket { get; } = socket;

        public PlayerSession Session { get; } = session;

        public GameEngine Engine { get; } = engine;

        /// <summary>
        /// "Something changed" signals; one pending signal is enough, because every send
        /// takes a fresh snapshot.
        /// </summary>
        public Channel<bool> StateChanges { get; } = Channel.CreateBounded<bool>(
            new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite, SingleReader = true });

        public async Task SendAsync<T>(T message, CancellationToken cancellationToken)
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(message, JsonOptions);
            await sendLock.WaitAsync(cancellationToken);
            try
            {
                if (Socket.State == WebSocketState.Open)
                {
                    await Socket.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
                }
            }
            finally
            {
                sendLock.Release();
            }
        }

        /// <summary>Starts the server's side of the close handshake; the receive loop then sees the client's answer.</summary>
        public async Task CloseOutputAsync(WebSocketCloseStatus status, string reason, CancellationToken cancellationToken)
        {
            await sendLock.WaitAsync(cancellationToken);
            try
            {
                if (Socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
                {
                    await Socket.CloseOutputAsync(status, reason, cancellationToken);
                }
            }
            finally
            {
                sendLock.Release();
            }
        }

        public void Dispose() => sendLock.Dispose();
    }

    public async Task HandleAsync(HttpContext context, string tableIdValue)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        var tableId = new TableId(tableIdValue);
        var token = context.Request.Query["token"].ToString();
        if (string.IsNullOrEmpty(token))
        {
            await HandleWithAuthMessageAsync(context, tableId);
            return;
        }

        // The token in the URL: everything is checked before the upgrade, so a refused client never holds a socket.
        if (sessionRegistry.Validate(token, tableId) is not { } session)
        {
            log.LogWarning("[WS] Missing or invalid token [tableId={TableId}]", tableId);
            await RefuseAsync(context, StatusCodes.Status401Unauthorized, "Invalid or expired session token");
            return;
        }
        if (tableRepository.Get(tableId) is not { } engine)
        {
            await RefuseAsync(context, StatusCodes.Status404NotFound, "Table not found");
            return;
        }
        if (!TryEnterTotal())
        {
            log.LogWarning("[WS] Connection limit reached ({MaxConnections})", options.Value.MaxConnections);
            await RefuseAsync(context, StatusCodes.Status503ServiceUnavailable, "Too many connections, try again later");
            return;
        }
        try
        {
            if (!TryEnterSession(token))
            {
                log.LogWarning("[WS] Too many connections of one session [playerId={PlayerId}]", session.PlayerId);
                await RefuseAsync(context, StatusCodes.Status429TooManyRequests, "Too many connections for this session");
                return;
            }
            try
            {
                using var socket = await context.WebSockets.AcceptWebSocketAsync();
                await ServeAsync(socket, session, engine, context.RequestAborted);
            }
            finally
            {
                ExitSession(token);
            }
        }
        finally
        {
            Interlocked.Decrement(ref connectionCount);
        }
    }

    /// <summary>The page's way: the token arrives as the first message, so it is checked after the upgrade.</summary>
    private async Task HandleWithAuthMessageAsync(HttpContext context, TableId tableId)
    {
        if (!TryEnterTotal())
        {
            log.LogWarning("[WS] Connection limit reached ({MaxConnections})", options.Value.MaxConnections);
            await RefuseAsync(context, StatusCodes.Status503ServiceUnavailable, "Too many connections, try again later");
            return;
        }
        try
        {
            var requestAborted = context.RequestAborted;
            using var socket = await context.WebSockets.AcceptWebSocketAsync();

            var receiving = ReceiveTextAsync(socket, requestAborted);
            if (await Task.WhenAny(receiving, Task.Delay(AuthTimeout, requestAborted)) != receiving)
            {
                log.LogWarning("[WS] No authentication within {Timeout} [tableId={TableId}]", AuthTimeout, tableId);
                await CloseWithoutWaitingAsync(socket, Unauthenticated, "Authentication timeout", receiving);
                return;
            }
            if (await receiving is not { } payload) return;  // the client closed the connection

            if (TryParseAuth(payload) is not { } token || sessionRegistry.Validate(token, tableId) is not { } session)
            {
                log.LogWarning("[WS] Missing or invalid token [tableId={TableId}]", tableId);
                await socket.CloseAsync(Unauthenticated, "Invalid or expired session token", requestAborted);
                return;
            }
            if (tableRepository.Get(tableId) is not { } engine)
            {
                await socket.CloseAsync(TableMissing, "Table not found", requestAborted);
                return;
            }
            if (!TryEnterSession(token))
            {
                log.LogWarning("[WS] Too many connections of one session [playerId={PlayerId}]", session.PlayerId);
                await socket.CloseAsync(TooManyConnections, "Too many connections for this session", requestAborted);
                return;
            }
            try
            {
                await ServeAsync(socket, session, engine, requestAborted);
            }
            finally
            {
                ExitSession(token);
            }
        }
        finally
        {
            Interlocked.Decrement(ref connectionCount);
        }
    }

    /// <summary>{"type":"Auth","token":"…"}.</summary>
    private sealed record AuthMessage(string? Type, string? Token);

    private static string? TryParseAuth(string payload)
    {
        try
        {
            return JsonSerializer.Deserialize<AuthMessage>(payload, JsonOptions) is { Type: "Auth", Token: { Length: > 0 } token } ? token : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Sends the close frame and gives the silent client a moment to answer it; the socket is disposed after.</summary>
    private static async Task CloseWithoutWaitingAsync(WebSocket socket, WebSocketCloseStatus status, string reason, Task pendingReceive)
    {
        try
        {
            using var timeout = new CancellationTokenSource(CloseHandshakeTimeout);
            await socket.CloseOutputAsync(status, reason, timeout.Token);
            await pendingReceive.WaitAsync(timeout.Token);
        }
        catch (Exception ex) when (ex is OperationCanceledException or WebSocketException)
        {
            // The client did not answer; disposing the socket aborts it.
        }
    }

    private async Task ServeAsync(WebSocket socket, PlayerSession session, GameEngine engine, CancellationToken requestAborted)
    {
        if (!tableRepository.Attach(session.TableId))
        {
            await socket.CloseAsync(TableMissing, "Table not found", requestAborted);
            return;
        }
        try
        {
            using var connection = new Connection(socket, session, engine);
            await RunAsync(connection, requestAborted);
        }
        finally
        {
            tableRepository.Detach(session.TableId);
        }
    }

    private async Task RunAsync(Connection connection, CancellationToken requestAborted)
    {
        var (session, engine) = (connection.Session, connection.Engine);
        log.LogInformation("[WS] Connected [playerId={PlayerId} tableId={TableId}]", session.PlayerId, session.TableId);

        using var stop = CancellationTokenSource.CreateLinkedTokenSource(requestAborted);
        void OnEngineEvent(GameEvent gameEvent) => connection.StateChanges.Writer.TryWrite(true);
        engine.EventPublished += OnEngineEvent;
        // A removed table: say goodbye, and give up on a client that does not answer the close.
        using var closing = engine.Closing.Register(() => _ = CloseForRemovedTableAsync(connection, stop));
        try
        {
            await SendSnapshotAsync(connection, stop.Token);
            var pushing = PushSnapshotsAsync(connection, stop.Token);
            await ReceiveActionsAsync(connection, stop.Token);

            await stop.CancelAsync();
            await pushing;
        }
        catch (Exception ex) when (ex is OperationCanceledException or WebSocketException or ObjectDisposedException)
        {
            log.LogInformation("[WS] Connection ended [playerId={PlayerId}]: {Reason}", session.PlayerId, ex.Message);
        }
        finally
        {
            engine.EventPublished -= OnEngineEvent;
            log.LogInformation("[WS] Disconnected [playerId={PlayerId} tableId={TableId} state={State}]",
                session.PlayerId, session.TableId, connection.Socket.State);
        }
    }

    private async Task CloseForRemovedTableAsync(Connection connection, CancellationTokenSource stop)
    {
        try
        {
            using var timeout = new CancellationTokenSource(CloseHandshakeTimeout);
            await connection.CloseOutputAsync(TableMissing, "Table closed", timeout.Token);
            stop.CancelAfter(CloseHandshakeTimeout);
        }
        catch (Exception ex) when (ex is OperationCanceledException or WebSocketException or ObjectDisposedException)
        {
            // The connection is already gone.
        }
    }

    private async Task PushSnapshotsAsync(Connection connection, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var _ in connection.StateChanges.Reader.ReadAllAsync(cancellationToken))
            {
                await SendSnapshotAsync(connection, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // The connection closed.
        }
    }

    /// <summary>Applies each incoming action until the client closes the connection.</summary>
    private async Task ReceiveActionsAsync(Connection connection, CancellationToken cancellationToken)
    {
        var socket = connection.Socket;
        while (await ReceiveTextAsync(socket, cancellationToken) is { } payload)
        {
            if (TryParseAction(payload) is not { } actionRequest)
            {
                log.LogWarning("[WS] Bad action payload [playerId={PlayerId} payload={Payload}]", connection.Session.PlayerId, Truncate(payload));
                await connection.SendAsync(new ActionErrorDto("Invalid action format"), cancellationToken);
                continue;
            }

            try
            {
                await connection.Engine.ApplyActionAsync(actionRequest.ToDomain(connection.Session.PlayerId), cancellationToken);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                // Not the player's turn, an invalid raise, … — reported, and the player may retry.
                log.LogWarning("[WS] Rejected action [playerId={PlayerId}]: {Message}", connection.Session.PlayerId, ex.Message);
                await connection.SendAsync(new ActionErrorDto(ex.Message), cancellationToken);
            }
        }

        if (socket.State == WebSocketState.CloseReceived)
        {
            await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, cancellationToken);
        }
    }

    private static ActionRequest? TryParseAction(string payload)
    {
        try
        {
            return JsonSerializer.Deserialize<ActionRequest>(payload, JsonOptions);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            return null;
        }
    }

    /// <returns>The next text message, or null once the client has closed the connection.</returns>
    private static async Task<string?> ReceiveTextAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        var buffer = new byte[MaxMessageBytes];
        var length = 0;
        while (true)
        {
            if (length == buffer.Length)
            {
                await socket.CloseAsync(WebSocketCloseStatus.MessageTooBig, "Message too big", cancellationToken);
                return null;
            }

            var result = await socket.ReceiveAsync(buffer.AsMemory(length), cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close) return null;

            length += result.Count;
            if (result.EndOfMessage) return Encoding.UTF8.GetString(buffer, 0, length);
        }
    }

    private static async Task SendSnapshotAsync(Connection connection, CancellationToken cancellationToken)
    {
        var snapshot = (await connection.Engine.SnapshotAsync(connection.Session.PlayerId, cancellationToken)).ToDto();
        await connection.SendAsync(snapshot, cancellationToken);
    }

    /// <summary>A visitor's payload goes into the log only as a short prefix.</summary>
    private static string Truncate(string payload) =>
        payload.Length <= MaxLoggedPayloadChars ? payload : $"{payload[..MaxLoggedPayloadChars]}… ({payload.Length} chars)";

    private static Task RefuseAsync(HttpContext context, int statusCode, string reason)
    {
        context.Response.StatusCode = statusCode;
        return context.Response.WriteAsync(reason);
    }

    // ── Connection limits ──

    private bool TryEnterTotal()
    {
        if (Interlocked.Increment(ref connectionCount) <= options.Value.MaxConnections) return true;
        Interlocked.Decrement(ref connectionCount);
        return false;
    }

    private bool TryEnterSession(string token)
    {
        var limit = options.Value.MaxConnectionsPerSession;
        while (true)
        {
            var current = connectionsPerSession.GetOrAdd(token, 0);
            if (current >= limit) return false;
            if (connectionsPerSession.TryUpdate(token, current + 1, current)) return true;
        }
    }

    private void ExitSession(string token)
    {
        while (true)
        {
            if (!connectionsPerSession.TryGetValue(token, out var current)) return;
            if (current <= 1)
            {
                if (connectionsPerSession.TryRemove(new KeyValuePair<string, int>(token, current))) return;
            }
            else if (connectionsPerSession.TryUpdate(token, current - 1, current))
            {
                return;
            }
        }
    }
}
