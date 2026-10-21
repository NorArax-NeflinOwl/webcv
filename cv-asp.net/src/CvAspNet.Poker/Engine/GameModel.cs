using CvAspNet.Poker.Evaluation;
using CvAspNet.Poker.Model;

namespace CvAspNet.Poker.Engine;

/// <summary>The hand's phase; a closed hierarchy — only the nested records exist.</summary>
public abstract record GameState
{
    private GameState()
    {
    }

    public sealed record WaitingForPlayers : GameState
    {
        public static WaitingForPlayers Instance { get; } = new();
    }

    public sealed record PreFlop(int Pot) : GameState;

    /// <summary>3 board cards.</summary>
    public sealed record Flop(int Pot, IReadOnlyList<Card> Board) : GameState;

    /// <summary>4 board cards.</summary>
    public sealed record Turn(int Pot, IReadOnlyList<Card> Board) : GameState;

    /// <summary>5 board cards.</summary>
    public sealed record River(int Pot, IReadOnlyList<Card> Board) : GameState;

    /// <summary>The hand is over; <see cref="Ranking"/> is empty when everybody else folded.</summary>
    public sealed record Showdown(int Pot, IReadOnlyList<Card> Board, IReadOnlyList<PlayerResult> Ranking) : GameState;
}

public abstract record PlayerAction(PlayerId PlayerId)
{
    public sealed record Fold(PlayerId PlayerId) : PlayerAction(PlayerId);

    public sealed record Check(PlayerId PlayerId) : PlayerAction(PlayerId);

    public sealed record Call(PlayerId PlayerId) : PlayerAction(PlayerId);

    /// <param name="Amount">The chips added now, on top of what the player already bet this round.</param>
    public sealed record Raise(PlayerId PlayerId, int Amount) : PlayerAction(PlayerId);
}

public abstract record GameEvent
{
    private GameEvent()
    {
    }

    public sealed record StateChanged(TableId TableId) : GameEvent;
}

/// <param name="HoleCards">Null while the cards are face down for the viewer.</param>
public sealed record PlayerView(
    PlayerId Id,
    string Name,
    int Coins,
    bool Folded,
    int BetThisRound,
    IReadOnlyList<Card>? HoleCards);

/// <param name="ToCall">What the viewing player has to pay to call; 0 for an observer.</param>
public sealed record TableView(
    GameState State,
    IReadOnlyList<Card> Board,
    int Pot,
    int ToCall,
    IReadOnlyList<PlayerView> Players,
    PlayerId? CurrentTurnId);
