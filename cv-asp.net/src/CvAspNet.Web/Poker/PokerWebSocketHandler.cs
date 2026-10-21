using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using CvAspNet.Poker.Engine;
using CvAspNet.Poker.Model;

namespace CvAspNet.Web.Poker;

/// <summary>
/// WebSocket endpoint /api/pokertable/{id}/ws?token=&lt;sessionToken&gt;.
/// <list type="bullet">
/// <item>Handshake: the token from POST /api/pokertable/{id}/join must belong to this table; otherwise the
/// connection is closed with 4001 (4004 when the table does not exist).</item>
/// <item>Server → client: after every state change, a <see cref="TableSnapshotDto"/> from this player's side
/// (opponents' hole cards hidden until the showdown).</item>
/// <item>Client → server: an <see cref="ActionRequest"/>; a rejected action is answered with
/// {"error": "…"} and the connection stays open, so the player can try again.</item>
/// </list>
/// </summary>
public sealed class PokerWebSocketHandler(TableRepository tableRepository, SessionRegistry sessionRegistry, ILogger<PokerWebSocketHandler> log)
{
    private const WebSocketCloseStatus Unauthenticated = (WebSocketCloseStatus)4001;
    private const WebSocketCloseStatus TableMissing = (WebSocketCloseStatus)4004;
    private const int MaxMessageBytes = 4 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        AllowOutOfOrderMetadataProperties = true,
        RespectRequiredConstructorParameters = true,
    };

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

        public void Dispose() => sendLock.Dispose();
    }

    public async Task HandleAsync(HttpContext context, string tableIdValue)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        var cancellationToken = context.RequestAborted;
        var tableId = new TableId(tableIdValue);

        var token = context.Request.Query["token"].ToString();
        if (string.IsNullOrWhiteSpace(token))
        {
            log.LogWarning("[WS] Missing token [tableId={TableId}]", tableId);
            await CloseAsync(socket, Unauthenticated, "Missing session token", cancellationToken);
            return;
        }
        if (sessionRegistry.Validate(token, tableId) is not { } session)
        {
            log.LogWarning("[WS] Invalid token [tableId={TableId}]", tableId);
            await CloseAsync(socket, Unauthenticated, "Invalid or expired session token", cancellationToken);
            return;
        }
        if (tableRepository.Get(tableId) is not { } engine)
        {
            await CloseAsync(socket, TableMissing, "Table not found", cancellationToken);
            return;
        }

        using var connection = new Connection(socket, session, engine);
        await RunAsync(connection, cancellationToken);
    }

    private async Task RunAsync(Connection connection, CancellationToken requestAborted)
    {
        var (session, engine) = (connection.Session, connection.Engine);
        log.LogInformation("[WS] Connected [playerId={PlayerId} tableId={TableId}]", session.PlayerId, session.TableId);

        using var stop = CancellationTokenSource.CreateLinkedTokenSource(requestAborted);
        void OnEngineEvent(GameEvent gameEvent) => connection.StateChanges.Writer.TryWrite(true);
        engine.EventPublished += OnEngineEvent;
        try
        {
            await SendSnapshotAsync(connection, stop.Token);
            var pushing = PushSnapshotsAsync(connection, stop.Token);
            await ReceiveActionsAsync(connection, stop.Token);

            await stop.CancelAsync();
            await pushing;
        }
        catch (Exception ex) when (ex is OperationCanceledException or WebSocketException)
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
                log.LogWarning("[WS] Bad action payload [playerId={PlayerId} payload={Payload}]", connection.Session.PlayerId, payload);
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

    private Task SendSnapshotAsync(Connection connection, CancellationToken cancellationToken)
    {
        var snapshot = connection.Engine.Snapshot(connection.Session.PlayerId).ToDto();
        return connection.SendAsync(snapshot, cancellationToken);
    }

    private static Task CloseAsync(WebSocket socket, WebSocketCloseStatus status, string reason, CancellationToken cancellationToken) =>
        socket.CloseAsync(status, reason, cancellationToken);
}
