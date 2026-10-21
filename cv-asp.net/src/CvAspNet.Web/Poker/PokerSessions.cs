using System.Collections.Concurrent;
using CvAspNet.Poker.Engine;
using CvAspNet.Poker.Model;

namespace CvAspNet.Web.Poker;

/// <summary>
/// Ties a session token to one player at one table.
/// </summary>
/// <param name="Token">Random bearer credential, passed as ?token=… on the REST and WebSocket URLs.</param>
public sealed record PlayerSession(string Token, PlayerId PlayerId, TableId TableId);

/// <summary>
/// In-memory registry of the active tables. Each <see cref="GameEngine"/> has its own lock, so concurrent
/// requests to different tables never wait for each other.
/// </summary>
public sealed class TableRepository(ILoggerFactory loggerFactory)
{
    private readonly ILogger<TableRepository> log = loggerFactory.CreateLogger<TableRepository>();
    private readonly ConcurrentDictionary<string, GameEngine> tables = new();

    public TableId Create(int blindAmount = 10)
    {
        var id = new TableId(Guid.NewGuid().ToString());
        tables[id.Value] = new GameEngine(id, blindAmount, logger: loggerFactory.CreateLogger<GameEngine>());
        log.LogInformation("Table created [tableId={TableId}] (blind={Blind})", id, blindAmount);
        return id;
    }

    public GameEngine? Get(TableId tableId)
    {
        if (tables.TryGetValue(tableId.Value, out var engine)) return engine;
        log.LogWarning("Table not found [tableId={TableId}]", tableId);
        return null;
    }

    /// <exception cref="KeyNotFoundException">No such table.</exception>
    public GameEngine GetOrThrow(TableId tableId) =>
        Get(tableId) ?? throw new KeyNotFoundException($"Table not found: {tableId}");

    /// <returns>True when the table existed.</returns>
    public bool Remove(TableId tableId)
    {
        var removed = tables.TryRemove(tableId.Value, out _);
        if (removed)
        {
            log.LogInformation("Table removed [tableId={TableId}]", tableId);
        }
        else
        {
            log.LogWarning("Attempted to remove non-existent table [tableId={TableId}]", tableId);
        }
        return removed;
    }

    public int Count => tables.Count;

    /// <summary>The ids of all active tables, in no particular order.</summary>
    public IReadOnlyList<TableId> AllTableIds() => tables.Keys.Select(key => new TableId(key)).ToList();
}

/// <summary>
/// In-memory map of bearer tokens to <see cref="PlayerSession"/>s; enough for a single node
/// (several nodes would need a shared store such as Redis).
/// </summary>
public sealed class SessionRegistry(ILogger<SessionRegistry> log)
{
    private readonly ConcurrentDictionary<string, PlayerSession> sessions = new();

    public string Create(PlayerId playerId, TableId tableId)
    {
        var token = Guid.NewGuid().ToString();
        sessions[token] = new PlayerSession(token, playerId, tableId);
        log.LogInformation("Session created [playerId={PlayerId} tableId={TableId}]", playerId, tableId);
        return token;
    }

    public PlayerSession? Get(string token) => sessions.GetValueOrDefault(token);

    /// <returns>The session, or null when the token is unknown or belongs to another table.</returns>
    public PlayerSession? Validate(string token, TableId tableId) =>
        sessions.TryGetValue(token, out var session) && session.TableId == tableId ? session : null;

    public void Revoke(string token)
    {
        if (sessions.TryRemove(token, out var removed))
        {
            log.LogInformation("Session revoked [playerId={PlayerId} tableId={TableId}]", removed.PlayerId, removed.TableId);
        }
    }
}
