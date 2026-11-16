namespace CvAspNet.Web.Poker;

/// <summary>
/// The limits of the in-memory poker tables (configuration section "Poker"). The API is public and
/// anonymous, so nothing a visitor starts may live or grow without bound.
/// </summary>
public sealed class PokerOptions
{
    public const string SectionName = "Poker";

    /// <summary>Tables in play at once; creating one more is answered with 503.</summary>
    public int MaxTables { get; set; } = 200;

    /// <summary>A table whose state has not changed for this long is removed with its sessions.</summary>
    public int IdleTableMinutes { get; set; } = 30;

    /// <summary>A table without an open WebSocket (never connected, or its player gone) for this long is removed.</summary>
    public int DisconnectGraceSeconds { get; set; } = 120;

    /// <summary>A human who does not act for this long checks, or folds when a bet is owed.</summary>
    public int TurnTimeoutSeconds { get; set; } = 60;

    /// <summary>How often the tables are checked for the limits above.</summary>
    public int SweepIntervalSeconds { get; set; } = 5;

    /// <summary>Open WebSockets per session token; one more is refused with 429.</summary>
    public int MaxConnectionsPerSession { get; set; } = 3;

    /// <summary>Open WebSockets in total; one more is refused with 503.</summary>
    public int MaxConnections { get; set; } = 600;

    public TimeSpan IdleTableTimeout => TimeSpan.FromMinutes(IdleTableMinutes);

    public TimeSpan DisconnectGrace => TimeSpan.FromSeconds(DisconnectGraceSeconds);

    public TimeSpan TurnTimeout => TimeSpan.FromSeconds(TurnTimeoutSeconds);

    public TimeSpan SweepInterval => TimeSpan.FromSeconds(SweepIntervalSeconds);
}
