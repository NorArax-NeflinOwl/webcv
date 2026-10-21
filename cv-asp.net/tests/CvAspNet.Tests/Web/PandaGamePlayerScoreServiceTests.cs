using CvAspNet.Web.Data;
using CvAspNet.Web.Models;
using CvAspNet.Web.Repositories;
using CvAspNet.Web.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace CvAspNet.Tests.Web;

public class PandaGamePlayerScoreServiceTests
{
    private readonly IPandaGamePlayerScoreRepository repository = Substitute.For<IPandaGamePlayerScoreRepository>();
    private readonly FixedTimeProvider timeProvider = new(new DateTimeOffset(2026, 6, 14, 8, 0, 0, TimeSpan.Zero));
    private readonly PandaGamePlayerScoreService service;

    public PandaGamePlayerScoreServiceTests()
    {
        service = new PandaGamePlayerScoreService(repository, timeProvider, NullLogger<PandaGamePlayerScoreService>.Instance);
        repository.SaveAsync(Arg.Any<PandaGamePlayerScore>(), Arg.Any<CancellationToken>()).Returns(call => call.Arg<PandaGamePlayerScore>());
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    // ── SaveScore: happy path ──

    [Fact]
    public async Task SaveScore_SavesTheScoreWithTheGivenNick()
    {
        var result = await service.SaveScoreAsync(new PandaGamePlayerScoreRequest("Player1", 123));

        Assert.Equal("Player1", result.Nick);
        Assert.Equal(123, result.Score);
        await repository.Received(1).SaveAsync(Arg.Is<PandaGamePlayerScore>(score => score.Nick == "Player1" && score.Score == 123), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveScore_StampsPolishLocalTime()
    {
        // 08:00 UTC in June is 10:00 in Warsaw (CEST).
        var result = await service.SaveScoreAsync(new PandaGamePlayerScoreRequest("Player1", 1));

        Assert.Equal("2026-06-14T10:00", result.PlayedAt);
    }

    [Fact]
    public async Task SaveScore_KeepsMicrosecondsLikeTheDatabase()
    {
        var precise = new PandaGamePlayerScoreService(
            repository,
            new FixedTimeProvider(new DateTimeOffset(2026, 6, 14, 8, 0, 7, TimeSpan.Zero).AddTicks(1_234_567)),
            NullLogger<PandaGamePlayerScoreService>.Instance);

        var result = await precise.SaveScoreAsync(new PandaGamePlayerScoreRequest("Player1", 1));

        Assert.Equal("2026-06-14T10:00:07.123456", result.PlayedAt);
    }

    [Fact]
    public async Task SaveScore_TrimsWhitespaceFromTheNick()
    {
        Assert.Equal("Player1", (await service.SaveScoreAsync(new PandaGamePlayerScoreRequest("  Player1  ", 10))).Nick);
    }

    [Fact]
    public async Task SaveScore_UsesAnonymousWhenTheNickIsBlank()
    {
        Assert.Equal("Anonymous", (await service.SaveScoreAsync(new PandaGamePlayerScoreRequest("   ", 50))).Nick);
    }

    [Fact]
    public async Task SaveScore_AcceptsZero()
    {
        Assert.Equal(0, (await service.SaveScoreAsync(new PandaGamePlayerScoreRequest("Player", 0))).Score);
    }

    [Fact]
    public async Task SaveScore_AcceptsTheMaximum()
    {
        var result = await service.SaveScoreAsync(new PandaGamePlayerScoreRequest("Player", PandaGamePlayerScoreService.MaxScore));

        Assert.Equal(PandaGamePlayerScoreService.MaxScore, result.Score);
    }

    // ── SaveScore: validation ──

    [Theory]
    [InlineData(-1)]
    [InlineData(PandaGamePlayerScoreService.MaxScore + 1)]
    public async Task SaveScore_RejectsAScoreOutOfRange(int score)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => service.SaveScoreAsync(new PandaGamePlayerScoreRequest("Player", score)));
        await repository.DidNotReceiveWithAnyArgs().SaveAsync(default!, default);
    }

    [Fact]
    public async Task SaveScore_RejectsANickThatIsTooLong()
    {
        var nick = new string('x', PandaGamePlayerScoreService.MaxNickLength + 1);

        await Assert.ThrowsAsync<ArgumentException>(() => service.SaveScoreAsync(new PandaGamePlayerScoreRequest(nick, 10)));
        await repository.DidNotReceiveWithAnyArgs().SaveAsync(default!, default);
    }

    // ── TopScores ──

    [Fact]
    public async Task TopScores_ReturnsTheRepositoryScores()
    {
        var playedAt = new DateTime(2026, 6, 14, 12, 0, 0);
        repository.FindTopAsync(PandaGamePlayerScoreService.TopScoresCount, Arg.Any<CancellationToken>()).Returns(
        [
            new PandaGamePlayerScore { Nick = "Player1", Score = 200, PlayedAt = playedAt },
            new PandaGamePlayerScore { Nick = "Player2", Score = 100, PlayedAt = playedAt },
        ]);

        var result = await service.TopScoresAsync();

        Assert.Equal(["Player1", "Player2"], result.Select(score => score.Nick));
        Assert.Equal(200, result[0].Score);
    }

    [Fact]
    public async Task TopScores_ReturnsAnEmptyListWhenThereAreNoScores()
    {
        repository.FindTopAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns([]);

        Assert.Empty(await service.TopScoresAsync());
    }

    // ── PlayedAt format (Java's LocalDateTime.toString) ──

    [Theory]
    [InlineData(0, 0, "2026-06-14T10:05")]
    [InlineData(7, 0, "2026-06-14T10:05:07")]
    [InlineData(7, 1_230_000, "2026-06-14T10:05:07.123")]
    [InlineData(7, 1_234_560, "2026-06-14T10:05:07.123456")]
    [InlineData(7, 1_234_567, "2026-06-14T10:05:07.123456700")]
    public void FormatLocalDateTime_WritesLikeJava(int second, long fractionTicks, string expected)
    {
        var value = new DateTime(2026, 6, 14, 10, 5, second).AddTicks(fractionTicks);

        Assert.Equal(expected, PandaGamePlayerScoreService.FormatLocalDateTime(value));
    }
}
