using System.Globalization;
using CvAspNet.Web.Data;
using CvAspNet.Web.Models;
using CvAspNet.Web.Repositories;

namespace CvAspNet.Web.Services;

public interface IPandaGamePlayerScoreService
{
    /// <exception cref="ArgumentException">The nick is too long or the score is out of range.</exception>
    Task<PandaGamePlayerScoreResponse> SaveScoreAsync(PandaGamePlayerScoreRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PandaGamePlayerScoreResponse>> TopScoresAsync(CancellationToken cancellationToken = default);
}

public sealed class PandaGamePlayerScoreService(
    IPandaGamePlayerScoreRepository repository,
    TimeProvider timeProvider,
    ILogger<PandaGamePlayerScoreService> log) : IPandaGamePlayerScoreService
{
    public const int MaxNickLength = 64;
    public const int MaxScore = 1_000_000;
    public const string DefaultNick = "Anonymous";
    public const int TopScoresCount = 10;

    /// <summary>Scores are stamped with Polish local time, whatever the server's time zone.</summary>
    public static readonly TimeZoneInfo PolishTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Warsaw");

    public async Task<PandaGamePlayerScoreResponse> SaveScoreAsync(PandaGamePlayerScoreRequest request, CancellationToken cancellationToken = default)
    {
        var nick = string.IsNullOrWhiteSpace(request.Nick) ? DefaultNick : request.Nick.Trim();

        if (nick.Length > MaxNickLength) throw new ArgumentException($"Nick cannot be longer than {MaxNickLength} characters");
        if (request.Score < 0) throw new ArgumentException("Score cannot be negative");
        if (request.Score > MaxScore) throw new ArgumentException($"Score cannot exceed {MaxScore}");

        var playedAt = TruncateToMicroseconds(TimeZoneInfo.ConvertTimeFromUtc(timeProvider.GetUtcNow().UtcDateTime, PolishTimeZone));
        var saved = await repository.SaveAsync(
            new PandaGamePlayerScore { Nick = nick, Score = request.Score, PlayedAt = playedAt },
            cancellationToken);

        log.LogDebug("Saved Panda score for nick={Nick}, score={Score}", nick, request.Score);
        return ToDto(saved);
    }

    public async Task<IReadOnlyList<PandaGamePlayerScoreResponse>> TopScoresAsync(CancellationToken cancellationToken = default) =>
        (await repository.FindTopAsync(TopScoresCount, cancellationToken)).Select(ToDto).ToList();

    /// <summary>PostgreSQL keeps microseconds: the saved score's answer then shows the time the list will show.</summary>
    private static DateTime TruncateToMicroseconds(DateTime value) => value.AddTicks(-(value.Ticks % TimeSpan.TicksPerMicrosecond));

    private static PandaGamePlayerScoreResponse ToDto(PandaGamePlayerScore score) =>
        new(score.Nick, score.Score, FormatLocalDateTime(score.PlayedAt));

    /// <summary>
    /// ISO-8601 local date-time the way Java's LocalDateTime.toString() writes it: seconds only when
    /// not zero, the fraction in groups of 3 digits only when not zero.
    /// </summary>
    internal static string FormatLocalDateTime(DateTime value)
    {
        var fractionTicks = value.Ticks % TimeSpan.TicksPerSecond;
        if (value.Second == 0 && fractionTicks == 0)
        {
            return value.ToString("yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture);
        }

        var dateTime = value.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);
        if (fractionTicks == 0) return dateTime;

        var nanoseconds = fractionTicks * 100;
        var fraction = nanoseconds % 1_000_000 == 0 ? (nanoseconds / 1_000_000).ToString("D3", CultureInfo.InvariantCulture)
            : nanoseconds % 1_000 == 0 ? (nanoseconds / 1_000).ToString("D6", CultureInfo.InvariantCulture)
            : nanoseconds.ToString("D9", CultureInfo.InvariantCulture);
        return $"{dateTime}.{fraction}";
    }
}
