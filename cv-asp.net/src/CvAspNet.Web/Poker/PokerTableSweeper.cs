using Microsoft.Extensions.Options;

namespace CvAspNet.Web.Poker;

/// <summary>
/// Every <see cref="PokerOptions.SweepIntervalSeconds"/>: plays the turns that have run out, then removes the
/// idle and abandoned tables and revokes their sessions, so the in-memory state cannot grow forever.
/// </summary>
public sealed class PokerTableSweeper(
    TableRepository tableRepository,
    SessionRegistry sessionRegistry,
    IOptions<PokerOptions> options,
    TimeProvider timeProvider,
    ILogger<PokerTableSweeper> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.SweepInterval, timeProvider);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await SweepAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    log.LogError(ex, "Poker table sweep failed");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // The application is stopping.
        }
    }

    /// <summary>One pass; also called by the tests.</summary>
    public async Task SweepAsync(CancellationToken cancellationToken = default)
    {
        foreach (var engine in tableRepository.All())
        {
            try
            {
                await engine.ExpireTurnAsync(options.Value.TurnTimeout, cancellationToken);
            }
            catch (InvalidOperationException)
            {
                // Removed (closed) while this pass was running.
            }
        }

        foreach (var tableId in tableRepository.RemoveExpired())
        {
            sessionRegistry.RevokeTable(tableId);
        }
    }
}
