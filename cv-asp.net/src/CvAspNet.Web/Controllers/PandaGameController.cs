using CvAspNet.Web.Models;
using CvAspNet.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CvAspNet.Web.Controllers;

[ApiController]
[Route("api/pandagame")]
[TypeFilter(typeof(PandaGameExceptionFilter))]
public sealed class PandaGameController(IPandaGamePlayerScoreService pandaGameService) : ControllerBase
{
    [HttpPost("scores")]
    public async Task<ActionResult<PandaGamePlayerScoreResponse>> SaveScore(PandaGamePlayerScoreRequest request, CancellationToken cancellationToken) =>
        StatusCode(StatusCodes.Status201Created, await pandaGameService.SaveScoreAsync(request, cancellationToken));

    [HttpGet("scores")]
    public Task<IReadOnlyList<PandaGamePlayerScoreResponse>> TopScores(CancellationToken cancellationToken) =>
        pandaGameService.TopScoresAsync(cancellationToken);
}

/// <summary>
/// Turns the score errors into <see cref="ErrorResponse"/>: invalid data (negative score, nick too long) is a 400
/// with the reason, anything else a 500. Malformed JSON is answered before the action runs (see Program.cs).
/// </summary>
internal sealed class PandaGameExceptionFilter(ILogger<PandaGameExceptionFilter> log) : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is ArgumentException invalidInput)
        {
            log.LogWarning("Invalid PandaGame score data: {Message}", invalidInput.Message);
            context.Result = new BadRequestObjectResult(new ErrorResponse(invalidInput.Message));
        }
        else
        {
            log.LogError(context.Exception, "Unexpected PandaGame error");
            context.Result = new ObjectResult(new ErrorResponse("Internal server error")) { StatusCode = StatusCodes.Status500InternalServerError };
        }
        context.ExceptionHandled = true;
    }
}
