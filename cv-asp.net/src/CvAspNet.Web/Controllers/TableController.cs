using CvAspNet.Poker.Model;
using CvAspNet.Poker.Strategy;
using CvAspNet.Web.Poker;
using Microsoft.AspNetCore.Mvc;

namespace CvAspNet.Web.Controllers;

/// <summary>
/// REST API of the poker tables.
/// <para>
/// Flow: POST /api/pokertable → tableId; POST /api/pokertable/{id}/join → sessionToken + playerId;
/// POST /api/pokertable/{id}/start?token=… → the first hand; WS /api/pokertable/{id}/ws?token=… (see PokerWebSocketHandler).
/// The session token is a bearer credential kept apart from cookies, so that the same token works for REST and WebSocket.
/// </para>
/// </summary>
[ApiController]
[Route("api/pokertable")]
public sealed class TableController(TableRepository tableRepository, SessionRegistry sessionRegistry) : ControllerBase
{
    private const int BotCount = 2;
    private const int BotCoins = 1_000;

    [HttpGet]
    public TableListResponse ListTables()
    {
        var ids = tableRepository.AllTableIds();
        return new TableListResponse(ids.Select(id => new TableInfoDto(id.Value)).ToList(), ids.Count);
    }

    /// <summary>Creates a table with two random bots already seated; the body is optional: {"blindAmount": 10}.</summary>
    [HttpPost]
    public ActionResult<CreateTableResponse> CreateTable(CreateTableRequest? body)
    {
        var tableId = tableRepository.Create(blindAmount: body?.BlindAmount ?? 10);
        var engine = tableRepository.GetOrThrow(tableId);

        // Pre-seated bots: the human only needs to join and start.
        for (var i = 1; i <= BotCount; i++)
        {
            engine.AddPlayer(
                new Player(new PlayerId(Guid.NewGuid().ToString()), $"Bot {i}", BotCoins, Hand.Empty, IsBot: true),
                new RandomBotStrategy());
        }

        return StatusCode(StatusCodes.Status201Created, new CreateTableResponse(tableId.Value));
    }

    /// <summary>Seats a human player and issues the session token for the WebSocket: {"playerName": "Alice", "coins": 1000}.</summary>
    [HttpPost("{id}/join")]
    public ActionResult<JoinTableResponse> JoinTable(string id, JoinTableRequest body)
    {
        var tableId = new TableId(id);
        if (tableRepository.Get(tableId) is not { } engine) return TableNotFound(id);

        var playerId = new PlayerId(Guid.NewGuid().ToString());
        engine.AddPlayer(new Player(playerId, body.PlayerName, body.Coins, Hand.Empty, IsBot: false));  // human: acts over the WebSocket

        var token = sessionRegistry.Create(playerId, tableId);
        return StatusCode(StatusCodes.Status201Created, new JoinTableResponse(token, playerId.Value));
    }

    /// <summary>Starts the first or the next hand; 409 while a hand is in progress or too few players have chips.</summary>
    [HttpPost("{id}/start")]
    public async Task<IActionResult> StartHand(string id, [FromQuery] string token, CancellationToken cancellationToken)
    {
        var tableId = new TableId(id);
        if (sessionRegistry.Validate(token, tableId) is null) return InvalidToken();
        if (tableRepository.Get(tableId) is not { } engine) return TableNotFound(id);

        try
        {
            await engine.StartHandAsync(cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(statusCode: StatusCodes.Status409Conflict, detail: ex.Message);
        }
        return NoContent();
    }

    /// <summary>The table from the requesting player's side; a polling fallback for clients without WebSocket.</summary>
    [HttpGet("{id}/snapshot")]
    public ActionResult<TableSnapshotDto> Snapshot(string id, [FromQuery] string token)
    {
        var tableId = new TableId(id);
        if (sessionRegistry.Validate(token, tableId) is not { } session) return InvalidToken();
        if (tableRepository.Get(tableId) is not { } engine) return TableNotFound(id);

        return engine.Snapshot(session.PlayerId).ToDto();
    }

    private ObjectResult TableNotFound(string id) => Problem(statusCode: StatusCodes.Status404NotFound, detail: $"Table not found: {id}");

    private ObjectResult InvalidToken() => Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Invalid or expired session token");
}
