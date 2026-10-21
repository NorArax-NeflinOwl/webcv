using System.Text.Json.Serialization;
using CvAspNet.Poker.Engine;
using CvAspNet.Poker.Evaluation;
using CvAspNet.Poker.Model;

namespace CvAspNet.Web.Poker;

// ── REST ──

public sealed record TableListResponse(IReadOnlyList<TableInfoDto> Tables, int Count);

public sealed record TableInfoDto(string TableId);

public sealed record CreateTableRequest(int BlindAmount = 10);

public sealed record CreateTableResponse(string TableId);

public sealed record JoinTableRequest(string PlayerName, int Coins = 1_000);

public sealed record JoinTableResponse(string SessionToken, string PlayerId);

// ── Server → client: the snapshot pushed over the WebSocket ──

/// <param name="Phase">WaitingForPlayers | PreFlop | Flop | Turn | River | Showdown.</param>
/// <param name="ToCall">What the requesting player has to pay to call.</param>
/// <param name="CurrentTurnId">The player to act, or null when no betting is going on.</param>
/// <param name="Ranking">Only at the showdown: the players, strongest hand first.</param>
public sealed record TableSnapshotDto(
    string Phase,
    int Pot,
    int ToCall,
    IReadOnlyList<CardDto> Board,
    IReadOnlyList<PlayerViewDto> Players,
    string? CurrentTurnId,
    IReadOnlyList<ShowdownResultDto>? Ranking);

/// <param name="Rank">TWO … ACE.</param>
/// <param name="Suit">GREEN_CLUB | BLUE_DIAMOND | RED_HEART | BLACK_SPADE.</param>
public sealed record CardDto(string Rank, string Suit);

/// <param name="HoleCards">Null while the cards are face down (an opponent before the showdown).</param>
public sealed record PlayerViewDto(string Id, string Name, int Coins, bool Folded, int BetThisRound, IReadOnlyList<CardDto>? HoleCards);

public sealed record ShowdownResultDto(string PlayerId, string PlayerName, string HandCategory);

/// <summary>Error pushed over the WebSocket when an action is rejected; the connection stays open.</summary>
public sealed record ActionErrorDto(string Error);

// ── Client → server: an action, with a "type" discriminator ──

/// <summary>
/// {"type":"Fold"}, {"type":"Check"}, {"type":"Call"} or {"type":"Raise","amount":50}.
/// The player is never taken from the payload: it is always the authenticated session's player.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(Fold), "Fold")]
[JsonDerivedType(typeof(Check), "Check")]
[JsonDerivedType(typeof(Call), "Call")]
[JsonDerivedType(typeof(Raise), "Raise")]
public abstract record ActionRequest
{
    public sealed record Fold : ActionRequest;

    public sealed record Check : ActionRequest;

    public sealed record Call : ActionRequest;

    public sealed record Raise(int Amount) : ActionRequest;

    public PlayerAction ToDomain(PlayerId playerId) => this switch
    {
        Fold => new PlayerAction.Fold(playerId),
        Check => new PlayerAction.Check(playerId),
        Call => new PlayerAction.Call(playerId),
        Raise raise => new PlayerAction.Raise(playerId, raise.Amount),
        _ => throw new InvalidOperationException($"Unknown action: {this}"),
    };
}

public static class PokerDtoMapping
{
    public static TableSnapshotDto ToDto(this TableView view) => new(
        Phase: PhaseName(view.State),
        Pot: view.Pot,
        ToCall: view.ToCall,
        Board: view.Board.Select(ToDto).ToList(),
        Players: view.Players.Select(player => new PlayerViewDto(
            player.Id.Value,
            player.Name,
            player.Coins,
            player.Folded,
            player.BetThisRound,
            player.HoleCards?.Select(ToDto).ToList())).ToList(),
        CurrentTurnId: view.CurrentTurnId?.Value,
        Ranking: view.State is GameState.Showdown showdown
            ? showdown.Ranking.Select(result => new ShowdownResultDto(
                result.PlayerId.Value,
                result.PlayerName,
                HandEvaluator.Evaluate(result.HandValue).Category.DisplayName())).ToList()
            : null);

    private static string PhaseName(GameState state) => state switch
    {
        GameState.WaitingForPlayers => "WaitingForPlayers",
        GameState.PreFlop => "PreFlop",
        GameState.Flop => "Flop",
        GameState.Turn => "Turn",
        GameState.River => "River",
        GameState.Showdown => "Showdown",
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, null),
    };

    private static CardDto ToDto(Card card) => new(RankName(card.Rank), SuitName(card.Suit));

    /// <summary>The names the page's card rendering expects (pokerGame.js RANK_LABEL).</summary>
    private static string RankName(Rank rank) => rank switch
    {
        Rank.Two => "TWO",
        Rank.Three => "THREE",
        Rank.Four => "FOUR",
        Rank.Five => "FIVE",
        Rank.Six => "SIX",
        Rank.Seven => "SEVEN",
        Rank.Eight => "EIGHT",
        Rank.Nine => "NINE",
        Rank.Ten => "TEN",
        Rank.Jack => "JACK",
        Rank.Queen => "QUEEN",
        Rank.King => "KING",
        Rank.Ace => "ACE",
        _ => throw new ArgumentOutOfRangeException(nameof(rank), rank, null),
    };

    /// <summary>The names the page's card rendering expects (pokerGame.js SUIT_META).</summary>
    private static string SuitName(Suit suit) => suit switch
    {
        Suit.GreenClub => "GREEN_CLUB",
        Suit.BlueDiamond => "BLUE_DIAMOND",
        Suit.RedHeart => "RED_HEART",
        Suit.BlackSpade => "BLACK_SPADE",
        _ => throw new ArgumentOutOfRangeException(nameof(suit), suit, null),
    };
}
