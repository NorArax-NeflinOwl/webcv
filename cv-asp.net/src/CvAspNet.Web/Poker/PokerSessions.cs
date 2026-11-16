using System.Collections.Concurrent;
using CvAspNet.Poker.Engine;
using CvAspNet.Poker.Model;
using Microsoft.Extensions.Options;

namespace CvAspNet.Web.Poker;

/// <summary>
/// Ties a session token to one player at one table.
/// </summary>
/// <param name="Token">Random bearer credential, passed as ?token=… on the REST and WebSocket URLs.</param>
public sealed record PlayerSession(string Token, PlayerId PlayerId, TableId TableId);

/// <summary>
/// In-memory registry of the active tables. Each <see cref="GameEngine"/> has its own lock, so concurrent
/// requests to different tables never wait for each other.
/// <para>
/// The number of tables is capped (<see cref="PokerOptions.MaxTables"/>), and <see cref="RemoveExpired"/>
/// drops the ones nobody uses any more; a removed table is closed, which ends its WebSockets.
/// </para>
/// </summary>
public sealed class TableRepository(ILoggerFactory loggerFactory, IOptions<PokerOptions> options, TimeProvider timeProvider)
{
    /// <summary>A table and its open WebSockets; <see cref="Connections"/> and <see cref="UnattendedSince"/> are guarded by the entry's lock.</summary>
    private sealed class Entry(GameEngine engine, DateTimeOffset createdAt)
    {
        public GameEngine Engine { get; } = engine;

        public int Connections { get; set; }

        /// <summary>Since when no WebSocket has been open: the creation, then the last disconnect.</summary>
        public DateTimeOffset UnattendedSince { get; set; } = createdAt;
    }

    private readonly ILogger<TableRepository> log = loggerFactory.CreateLogger<TableRepository>();
    private readonly ConcurrentDictionary<string, Entry> tables = new();
    private readonly Lock createLock = new();

    /// <summary>A table with one human seat: the page seats its player against bots.</summary>
    /// <returns>The new table, or null when <see cref="PokerOptions.MaxTables"/> tables are in play.</returns>
    public TableId? Create(int blindAmount = 10)
    {
        var id = new TableId(Guid.NewGuid().ToString());
        lock (createLock)
        {
            if (tables.Count >= options.Value.MaxTables)
            {
                log.LogWarning("Table limit reached ({MaxTables}): table not created", options.Value.MaxTables);
                return null;
            }
            var engine = new GameEngine(id, blindAmount, logger: loggerFactory.CreateLogger<GameEngine>(), maxHumanSeats: 1, timeProvider: timeProvider);
            tables[id.Value] = new Entry(engine, timeProvider.GetUtcNow());
        }
        log.LogInformation("Table created [tableId={TableId}] (blind={Blind})", id, blindAmount);
        return id;
    }

    public GameEngine? Get(TableId tableId)
    {
        if (tables.TryGetValue(tableId.Value, out var entry)) return entry.Engine;
        log.LogWarning("Table not found [tableId={TableId}]", tableId);
        return null;
    }

    /// <exception cref="KeyNotFoundException">No such table.</exception>
    public GameEngine GetOrThrow(TableId tableId) =>
        Get(tableId) ?? throw new KeyNotFoundException($"Table not found: {tableId}");

    /// <summary>Every table in play, in no particular order.</summary>
    public IReadOnlyList<GameEngine> All() => tables.Values.Select(entry => entry.Engine).ToList();

    /// <summary>Counts a WebSocket opened at the table.</summary>
    /// <returns>False when the table is gone.</returns>
    public bool Attach(TableId tableId)
    {
        if (!tables.TryGetValue(tableId.Value, out var entry)) return false;
        lock (entry)
        {
            entry.Connections++;
        }
        return true;
    }

    /// <summary>Counts a WebSocket closed; the last one starts the <see cref="PokerOptions.DisconnectGraceSeconds"/>.</summary>
    public void Detach(TableId tableId)
    {
        if (!tables.TryGetValue(tableId.Value, out var entry)) return;
        lock (entry)
        {
            if (--entry.Connections == 0) entry.UnattendedSince = timeProvider.GetUtcNow();
        }
    }

    /// <summary>
    /// Removes and closes the tables whose state has not changed for <see cref="PokerOptions.IdleTableMinutes"/>,
    /// and those without an open WebSocket for <see cref="PokerOptions.DisconnectGraceSeconds"/>.
    /// </summary>
    /// <returns>The removed tables, whose sessions the caller revokes.</returns>
    public IReadOnlyList<TableId> RemoveExpired()
    {
        var now = timeProvider.GetUtcNow();
        var limits = options.Value;
        List<TableId> removed = [];
        foreach (var entry in tables.Values)
        {
            bool expired;
            lock (entry)
            {
                expired = now - entry.Engine.LastActivity >= limits.IdleTableTimeout
                          || (entry.Connections == 0 && now - entry.UnattendedSince >= limits.DisconnectGrace);
            }
            if (expired && Remove(entry.Engine.TableId)) removed.Add(entry.Engine.TableId);
        }
        return removed;
    }

    /// <summary>Removes the table and closes it, which ends its WebSockets.</summary>
    /// <returns>True when the table existed.</returns>
    public bool Remove(TableId tableId)
    {
        if (!tables.TryRemove(tableId.Value, out var entry))
        {
            log.LogWarning("Attempted to remove non-existent table [tableId={TableId}]", tableId);
            return false;
        }
        entry.Engine.Close();
        log.LogInformation("Table removed [tableId={TableId}]", tableId);
        return true;
    }

    public int Count => tables.Count;
}

/// <summary>
/// In-memory map of bearer tokens to <see cref="PlayerSession"/>s; enough for a single node
/// (several nodes would need a shared store such as Redis). A session lives as long as its table:
/// there is one per human seat, so <see cref="PokerOptions.MaxTables"/> caps them too.
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

    /// <summary>Revokes every session of a removed table.</summary>
    public void RevokeTable(TableId tableId)
    {
        foreach (var session in sessions.Values.Where(session => session.TableId == tableId))
        {
            Revoke(session.Token);
        }
    }

    public int Count => sessions.Count;
}
