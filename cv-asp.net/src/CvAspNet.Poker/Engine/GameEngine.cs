using CvAspNet.Poker.Evaluation;
using CvAspNet.Poker.Model;
using CvAspNet.Poker.Strategy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CvAspNet.Poker.Engine;

/// <summary>
/// One Texas Hold'em table: seats, dealing, betting rounds, showdown and the bots' moves.
/// <para>
/// Every operation runs under the table's own lock. State changes are announced through
/// <see cref="EventPublished"/> (Observer pattern: the WebSocket layer pushes a snapshot to each
/// connected player); the events are raised after the lock is released, so a listener may call
/// back into the engine.
/// </para>
/// </summary>
public sealed class GameEngine
{
    private const int HoleCardCount = 2;
    private const int FlopCardCount = 3;

    private sealed record Seat(Player Player, IBotStrategy? Strategy)
    {
        public bool IsHuman => Strategy is null;
    }

    /// <summary>Everything dealt anew with each hand; replaced as a whole when a hand starts.</summary>
    private sealed class HandState(Deck deck)
    {
        public Deck Deck { get; } = deck;

        public List<Player> Players { get; set; } = [];

        public IReadOnlyList<Card> Board { get; set; } = [];

        public int Pot { get; set; }

        public int CurrentBet { get; set; }

        public Dictionary<PlayerId, int> BetsThisRound { get; } = [];

        /// <summary>The players still to act in this betting round; the first one holds the turn.</summary>
        public List<PlayerId> ActionQueue { get; set; } = [];
    }

    private readonly int blindAmount;
    private readonly Func<Deck> deckFactory;
    private readonly ILogger<GameEngine> log;
    private readonly List<Seat> seats = [];
    private readonly SemaphoreSlim tableLock = new(1, 1);
    private readonly List<GameEvent> pendingEvents = [];

    private GameState gameState = GameState.WaitingForPlayers.Instance;
    private HandState hand = new(Deck.Of());
    private int dealerIndex;

    /// <param name="deckFactory">Supplies each hand's deck; tests pass a fixed card order.</param>
    public GameEngine(TableId tableId, int blindAmount = 10, Func<Deck>? deckFactory = null, ILogger<GameEngine>? logger = null)
    {
        TableId = tableId;
        this.blindAmount = blindAmount;
        this.deckFactory = deckFactory ?? Deck.Shuffle;
        log = logger ?? NullLogger<GameEngine>.Instance;
    }

    public TableId TableId { get; }

    /// <summary>Raised once per state change, after the operation that caused it has released the table.</summary>
    public event Action<GameEvent>? EventPublished;

    // ── Seat management ──

    /// <param name="strategy">Null seats a human, who acts through <see cref="ApplyActionAsync"/>.</param>
    public void AddPlayer(Player player, IBotStrategy? strategy = null) => RunLocked(() =>
    {
        if (seats.Any(seat => seat.Player.Id == player.Id))
        {
            throw new ArgumentException($"Player {player.Name} is already seated at this table");
        }
        seats.Add(new Seat(player, strategy));

        var type = strategy is null ? "human" : $"bot[{strategy.GetType().Name}]";
        log.LogInformation("[{TableId}] Player joined: {Name} ({Type}) | coins={Coins}", TableId, player.Name, type, player.Coins);
    });

    public void RemovePlayer(PlayerId playerId) => RunLocked(() =>
    {
        var removed = seats.Find(seat => seat.Player.Id == playerId);
        if (removed is null)
        {
            log.LogWarning("[{TableId}] RemovePlayer: player not found [playerId={PlayerId}]", TableId, playerId);
            return;
        }
        seats.Remove(removed);
        log.LogInformation("[{TableId}] Player left: {Name}", TableId, removed.Player.Name);
    });

    // ── Game flow ──

    /// <summary>
    /// Starts a new hand: shuffles, deals hole cards, posts the blinds, builds the action queue,
    /// then plays the bots until a human's turn.
    /// </summary>
    /// <exception cref="InvalidOperationException">Fewer than 2 players with chips, or a hand is in progress.</exception>
    public Task StartHandAsync(CancellationToken cancellationToken = default) => RunLockedAsync(() =>
    {
        StartHand();
        ProcessBotActions();
    }, cancellationToken);

    /// <summary>Applies a human player's move, then plays the bots until the next human turn or the end of the hand.</summary>
    /// <exception cref="InvalidOperationException">Not this player's turn, or the move is not allowed now.</exception>
    /// <exception cref="ArgumentException">The raise amount is invalid.</exception>
    public Task ApplyActionAsync(PlayerAction action, CancellationToken cancellationToken = default) => RunLockedAsync(() =>
    {
        if (gameState is GameState.WaitingForPlayers or GameState.Showdown)
        {
            throw new InvalidOperationException("No active betting round");
        }
        ApplyAction(action);
        ProcessBotActions();
    }, cancellationToken);

    /// <summary>
    /// The table as one player sees it: opponents' hole cards stay hidden (null) until the showdown.
    /// </summary>
    /// <param name="forPlayer">The viewer; null for an observer, who sees no hole cards.</param>
    public TableView Snapshot(PlayerId? forPlayer = null)
    {
        tableLock.Wait();
        try
        {
            return BuildSnapshot(forPlayer);
        }
        finally
        {
            tableLock.Release();
        }
    }

    // ── Hand start ──

    private void StartHand()
    {
        if (seats.Count < 2) throw new InvalidOperationException("At least 2 players are required to start a hand");
        if (gameState is not (GameState.WaitingForPlayers or GameState.Showdown))
        {
            throw new InvalidOperationException("Cannot start a new hand while a round is in progress");
        }

        var seatsWithChips = seats.Where(seat => seat.Player.Coins > 0).ToList();
        if (seatsWithChips.Count < 2) throw new InvalidOperationException("Not enough players with chips");

        hand = new HandState(deckFactory());
        hand.Players = seatsWithChips
            .Select(seat => seat.Player with { HoleCards = new Hand(hand.Deck.DrawMany(HoleCardCount)), Folded = false })
            .ToList();

        // Small blind: the player after the dealer; big blind: the player after the small blind.
        var playerCount = hand.Players.Count;
        var smallBlind = hand.Players[(dealerIndex + 1) % playerCount];
        var bigBlindIndex = (dealerIndex + 2) % playerCount;
        PostBlind(smallBlind.Id, blindAmount / 2);
        PostBlind(hand.Players[bigBlindIndex].Id, blindAmount);
        hand.CurrentBet = blindAmount;

        // Pre-flop the action starts with the player after the big blind (UTG).
        hand.ActionQueue = BuildActionQueue(startIndex: (bigBlindIndex + 1) % playerCount);

        log.LogInformation(
            "[{TableId}] ── New hand ── players={Players} SB={SmallBlind} BB={BigBlind} blind={Blind}",
            TableId, hand.Players.Select(player => player.Name), smallBlind.Name, hand.Players[bigBlindIndex].Name, blindAmount);

        gameState = new GameState.PreFlop(hand.Pot);
        Publish();
    }

    // ── Actions ──

    private void ApplyAction(PlayerAction action)
    {
        if (hand.ActionQueue.Count == 0) throw new InvalidOperationException("No player is waiting to act");
        var currentTurn = hand.ActionQueue[0];
        if (action.PlayerId != currentTurn)
        {
            throw new InvalidOperationException($"Not player {action.PlayerId}'s turn (current turn: {currentTurn})");
        }

        var player = RequireActivePlayer(action.PlayerId);
        var handOver = action switch
        {
            PlayerAction.Fold => ApplyFold(player),
            PlayerAction.Check => ApplyCheck(player),
            PlayerAction.Call => ApplyCall(player),
            PlayerAction.Raise raise => ApplyRaise(player, raise.Amount),
            _ => throw new ArgumentException($"Unknown action: {action}"),
        };
        if (handOver) return;

        // The betting round ends when nobody is left to act.
        if (hand.ActionQueue.Count == 0)
        {
            AdvancePhase();
        }
        else
        {
            Publish();
        }
    }

    /// <returns>True when the fold ended the hand.</returns>
    private bool ApplyFold(Player player)
    {
        log.LogDebug("[{TableId}] {Name} FOLD | phase={Phase}", TableId, player.Name, PhaseName);
        ReplacePlayer(player.Id, current => current with { Folded = true });
        hand.ActionQueue.RemoveAt(0);

        // The last player still in wins the pot at once.
        var remaining = ActivePlayers();
        if (remaining.Count != 1) return false;

        log.LogInformation("[{TableId}] All others folded — {Name} wins | pot={Pot}", TableId, remaining[0].Name, hand.Pot);
        AwardPot([remaining[0].Id]);
        FinishHand([]);
        return true;
    }

    private bool ApplyCheck(Player player)
    {
        var owed = ToCall(player.Id);
        if (owed != 0) throw new InvalidOperationException($"Cannot check — player must call {owed} chips or fold");

        log.LogDebug("[{TableId}] {Name} CHECK | phase={Phase}", TableId, player.Name, PhaseName);
        hand.ActionQueue.RemoveAt(0);
        return false;
    }

    private bool ApplyCall(Player player)
    {
        // Matches the current bet; a player without enough chips goes all-in.
        var owed = Math.Min(ToCall(player.Id), player.Coins);
        log.LogDebug("[{TableId}] {Name} CALL {Owed} | pot={Pot} phase={Phase}", TableId, player.Name, owed, hand.Pot + owed, PhaseName);
        PlaceBet(player.Id, owed);
        hand.ActionQueue.RemoveAt(0);
        return false;
    }

    private bool ApplyRaise(Player player, int amount)
    {
        var totalContribution = BetThisRound(player.Id) + amount;
        if (totalContribution <= hand.CurrentBet)
        {
            throw new ArgumentException($"Raise total ({totalContribution}) must exceed the current bet ({hand.CurrentBet})");
        }
        if (amount > player.Coins) throw new ArgumentException($"Not enough chips: {player.Coins} < {amount}");

        log.LogDebug(
            "[{TableId}] {Name} RAISE {Amount} (total={Total}) | pot={Pot} phase={Phase}",
            TableId, player.Name, amount, totalContribution, hand.Pot + amount, PhaseName);
        PlaceBet(player.Id, amount);
        hand.CurrentBet = BetThisRound(player.Id);

        // Everybody else still in must act again after a raise.
        RebuildQueueAfterRaise(player.Id);
        return false;
    }

    // ── Phase transitions ──

    private void AdvancePhase()
    {
        hand.CurrentBet = 0;
        hand.BetsThisRound.Clear();

        switch (gameState)
        {
            case GameState.PreFlop:
                hand.Board = hand.Deck.DrawMany(FlopCardCount);
                ResetPostFlopQueue();
                log.LogInformation("[{TableId}] ── FLOP: {Board} | pot={Pot}", TableId, hand.Board, hand.Pot);
                gameState = new GameState.Flop(hand.Pot, hand.Board);
                break;
            case GameState.Flop:
                DealOneBoardCard();
                log.LogInformation("[{TableId}] ── TURN: {Card} | pot={Pot}", TableId, hand.Board[^1], hand.Pot);
                gameState = new GameState.Turn(hand.Pot, hand.Board);
                break;
            case GameState.Turn:
                DealOneBoardCard();
                log.LogInformation("[{TableId}] ── RIVER: {Card} | pot={Pot}", TableId, hand.Board[^1], hand.Pot);
                gameState = new GameState.River(hand.Pot, hand.Board);
                break;
            case GameState.River:
                Showdown();
                return;  // FinishHand sets the state and publishes it
            default:
                throw new InvalidOperationException($"Cannot advance phase from: {gameState}");
        }

        Publish();
    }

    private void DealOneBoardCard()
    {
        hand.Board = [.. hand.Board, hand.Deck.Draw()];
        ResetPostFlopQueue();
    }

    private void Showdown()
    {
        var ranking = EvaluateShowdown();
        var winners = FindWinners(ranking);
        if (winners.Count == 1)
        {
            log.LogInformation("[{TableId}] ── SHOWDOWN | winner={Winner} | ranking={Ranking}", TableId, ranking[0].PlayerName, ranking.Select(result => result.PlayerName));
        }
        else
        {
            log.LogInformation("[{TableId}] ── SHOWDOWN tie | winners={Winners} | ranking={Ranking}", TableId, winners, ranking.Select(result => result.PlayerName));
        }
        AwardPot(winners);
        FinishHand(ranking);
    }

    // ── Bot auto-play ──

    /// <summary>Plays bots in turn until the queue is empty, a human is to act, or the hand is over.</summary>
    private void ProcessBotActions()
    {
        while (gameState is not (GameState.WaitingForPlayers or GameState.Showdown) && hand.ActionQueue.Count > 0)
        {
            var nextId = hand.ActionQueue[0];
            var strategy = seats.Find(seat => seat.Player.Id == nextId)?.Strategy;
            if (strategy is null) return;  // a human: wait for ApplyActionAsync

            var view = BuildSnapshot(nextId);
            var playerView = view.Players.First(player => player.Id == nextId);
            ApplyAction(strategy.Decide(view, playerView));
        }
    }

    // ── Helpers ──

    private string PhaseName => gameState.GetType().Name;

    private List<Player> ActivePlayers() => hand.Players.Where(player => !player.Folded).ToList();

    private Player RequireActivePlayer(PlayerId id) =>
        hand.Players.Find(player => player.Id == id && !player.Folded)
        ?? throw new InvalidOperationException($"Player {id} is not active or has folded");

    private int BetThisRound(PlayerId playerId) => hand.BetsThisRound.GetValueOrDefault(playerId);

    private int ToCall(PlayerId playerId) => Math.Max(0, hand.CurrentBet - BetThisRound(playerId));

    /// <summary>A forced bet: no action validation, capped at the player's chips.</summary>
    private void PostBlind(PlayerId playerId, int amount)
    {
        var actual = Math.Min(amount, hand.Players.First(player => player.Id == playerId).Coins);
        PlaceBet(playerId, actual);
    }

    private void PlaceBet(PlayerId playerId, int amount)
    {
        ReplacePlayer(playerId, player => player with { Coins = player.Coins - amount });
        hand.BetsThisRound[playerId] = BetThisRound(playerId) + amount;
        hand.Pot += amount;
    }

    private void ReplacePlayer(PlayerId playerId, Func<Player, Player> update)
    {
        var index = hand.Players.FindIndex(player => player.Id == playerId);
        hand.Players[index] = update(hand.Players[index]);
    }

    /// <summary>The not-folded players, starting at <paramref name="startIndex"/> (inclusive) and wrapping around.</summary>
    private List<PlayerId> BuildActionQueue(int startIndex)
    {
        var playerCount = hand.Players.Count;
        return Enumerable.Range(startIndex, playerCount)
            .Select(index => hand.Players[index % playerCount])
            .Where(player => !player.Folded)
            .Select(player => player.Id)
            .ToList();
    }

    /// <summary>Post-flop the first active player left of the dealer acts first.</summary>
    private void ResetPostFlopQueue() => hand.ActionQueue = BuildActionQueue((dealerIndex + 1) % hand.Players.Count);

    /// <summary>Everybody after the raiser acts again; the raiser does not re-act on their own raise.</summary>
    private void RebuildQueueAfterRaise(PlayerId raiserId)
    {
        var raiserIndex = hand.Players.FindIndex(player => player.Id == raiserId);
        var queue = BuildActionQueue((raiserIndex + 1) % hand.Players.Count);
        queue.Remove(raiserId);
        hand.ActionQueue = queue;
    }

    /// <summary>Each active player's best hand (hole cards + board), strongest first.</summary>
    private List<PlayerResult> EvaluateShowdown() =>
        ActivePlayers()
            .Select(player => new PlayerResult(player.Id, player.Name, new Hand([.. player.HoleCards.Cards, .. hand.Board])))
            .OrderDescending()
            .ToList();

    /// <summary>Everybody whose hand equals the strongest one; <paramref name="ranking"/> is strongest first.</summary>
    private static List<PlayerId> FindWinners(List<PlayerResult> ranking)
    {
        if (ranking.Count == 0) return [];
        var best = ranking[0];
        return ranking.TakeWhile(result => result.CompareTo(best) == 0).Select(result => result.PlayerId).ToList();
    }

    /// <summary>Splits the pot evenly; the remainder of an uneven split goes to the first winner.</summary>
    private void AwardPot(List<PlayerId> winnerIds)
    {
        if (winnerIds.Count == 0) return;

        var share = hand.Pot / winnerIds.Count;
        var remainder = hand.Pot % winnerIds.Count;
        for (var i = 0; i < winnerIds.Count; i++)
        {
            var amount = share + (i == 0 ? remainder : 0);
            var name = hand.Players.Find(player => player.Id == winnerIds[i])?.Name ?? winnerIds[i].Value;
            log.LogInformation("[{TableId}] {Name} receives {Amount} from pot", TableId, name, amount);
            ReplacePlayer(winnerIds[i], player => player with { Coins = player.Coins + amount });
        }

        // The seats carry the chip counts over to the next hand.
        for (var i = 0; i < seats.Count; i++)
        {
            var updated = hand.Players.Find(player => player.Id == seats[i].Player.Id);
            if (updated is not null) seats[i] = seats[i] with { Player = seats[i].Player with { Coins = updated.Coins } };
        }
        hand.Pot = 0;
    }

    /// <summary>Sets the final state of the hand and moves the dealer button.</summary>
    private void FinishHand(List<PlayerResult> ranking)
    {
        dealerIndex = (dealerIndex + 1) % seats.Count;
        hand.ActionQueue.Clear();
        gameState = new GameState.Showdown(hand.Pot, hand.Board, ranking);
        Publish();
    }

    private TableView BuildSnapshot(PlayerId? forPlayer) => new(
        State: gameState,
        Board: hand.Board,
        Pot: hand.Pot,
        ToCall: forPlayer is { } viewer ? ToCall(viewer) : 0,
        Players: hand.Players.Select(player => new PlayerView(
            player.Id,
            player.Name,
            player.Coins,
            player.Folded,
            BetThisRound(player.Id),
            HoleCards: player.Id == forPlayer || gameState is GameState.Showdown ? player.HoleCards.Cards : null)).ToList(),
        CurrentTurnId: hand.ActionQueue.Count > 0 ? hand.ActionQueue[0] : null);

    private void Publish() => pendingEvents.Add(new GameEvent.StateChanged(TableId));

    // ── Locking ──

    private void RunLocked(Action operation)
    {
        tableLock.Wait();
        try
        {
            operation();
        }
        finally
        {
            tableLock.Release();
        }
    }

    private async Task RunLockedAsync(Action operation, CancellationToken cancellationToken)
    {
        await tableLock.WaitAsync(cancellationToken);
        try
        {
            operation();
        }
        finally
        {
            // Whatever changed before a failure is announced too.
            List<GameEvent> published = [.. pendingEvents];
            pendingEvents.Clear();
            tableLock.Release();
            foreach (var gameEvent in published)
            {
                EventPublished?.Invoke(gameEvent);
            }
        }
    }
}
