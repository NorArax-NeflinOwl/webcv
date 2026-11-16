using System.Threading.RateLimiting;
using CvAspNet.Web.Models;
using Microsoft.AspNetCore.RateLimiting;

namespace CvAspNet.Web;

/// <summary>
/// Per-visitor limits on the anonymous requests that create something (configuration section "RateLimits"):
/// a poker table, a seat, a Panda score. The visitor is the client address, which behind the gateway comes from
/// X-Forwarded-For (see <see cref="ForwardedHeadersSetup"/>). Beyond a limit the answer is 429 with
/// {"error": "…"}, which both pages show.
/// </summary>
public sealed class RateLimits
{
    public const string SectionName = "RateLimits";
    public const string PokerTables = "poker-tables";
    public const string PokerJoins = "poker-joins";
    public const string PandaScores = "panda-scores";

    public int PokerTablesPerMinute { get; set; } = 10;

    public int PokerJoinsPerMinute { get; set; } = 20;

    public int PandaScoresPerMinute { get; set; } = 10;

    public static void AddTo(IServiceCollection services, RateLimits limits) => services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.OnRejected = (context, cancellationToken) =>
        {
            if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            {
                context.HttpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            return new ValueTask(context.HttpContext.Response.WriteAsJsonAsync(
                new ErrorResponse("Too many requests, please try again in a minute"), cancellationToken));
        };
        options.AddPolicy(PokerTables, context => PerClientPerMinute(context, limits.PokerTablesPerMinute));
        options.AddPolicy(PokerJoins, context => PerClientPerMinute(context, limits.PokerJoinsPerMinute));
        options.AddPolicy(PandaScores, context => PerClientPerMinute(context, limits.PandaScoresPerMinute));
    });

    private static RateLimitPartition<string> PerClientPerMinute(HttpContext context, int permits) =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = permits, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 });
}
