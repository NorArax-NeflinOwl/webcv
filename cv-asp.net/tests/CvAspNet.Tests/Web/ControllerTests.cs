using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CvAspNet.Web.Controllers;
using CvAspNet.Web.Models;
using CvAspNet.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace CvAspNet.Tests.Web;

public sealed class ProfileControllerTests : IDisposable
{
    private readonly IProfileService profileService = Substitute.For<IProfileService>();
    private readonly CvApplicationFactory factory;
    private readonly HttpClient client;

    public ProfileControllerTests()
    {
        factory = new CvApplicationFactory
        {
            ConfigureServices = services => services.Replace(ServiceDescriptor.Singleton(profileService)),
        };
        client = factory.CreateClient();
        profileService.GetProfileAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(BuildProfile());
    }

    public void Dispose() => factory.Dispose();

    private static ProfileDto BuildProfile(string footerStatus = "YELLOW", IReadOnlyList<ExperienceDto>? experiences = null) => new(
        Title: "Jan Kowalski  Backend Developer",
        Eyebrow: "backend developer",
        Name: "Jan Kowalski",
        Role: "<span>Kotlin</span>",
        Bio: "Test bio",
        Links: new Dictionary<string, string> { ["GithubLink"] = "https://github.com/test", ["E-mail"] = "test@example.com" },
        Technologies: new Dictionary<string, string> { ["Kotlin"] = "primary language" },
        Stacks: ["JVM 17", "Docker"],
        Footer: "available",
        FooterStatus: footerStatus,
        Experiences: experiences ?? []);

    private async Task<JsonElement> GetProfileJsonAsync(string path = "/api/profileinfo")
    {
        var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task GetProfileInfo_ReturnsTheFieldsAsCamelCaseJson()
    {
        var json = await GetProfileJsonAsync();

        Assert.Equal("Jan Kowalski", json.GetProperty("name").GetString());
        Assert.Equal("backend developer", json.GetProperty("eyebrow").GetString());
        Assert.Equal("Jan Kowalski  Backend Developer", json.GetProperty("title").GetString());
        Assert.Equal("<span>Kotlin</span>", json.GetProperty("role").GetString());
        Assert.Equal("Test bio", json.GetProperty("bio").GetString());
        Assert.Equal("available", json.GetProperty("footer").GetString());
        Assert.Equal("YELLOW", json.GetProperty("footerStatus").GetString());
    }

    [Fact]
    public async Task GetProfileInfo_KeepsTheLinkAndTechnologyKeysAsTheyAre()
    {
        var json = await GetProfileJsonAsync();

        Assert.Equal("https://github.com/test", json.GetProperty("links").GetProperty("GithubLink").GetString());
        Assert.Equal("test@example.com", json.GetProperty("links").GetProperty("E-mail").GetString());
        Assert.Equal("primary language", json.GetProperty("technologies").GetProperty("Kotlin").GetString());
    }

    [Fact]
    public async Task GetProfileInfo_ReturnsStacksAsAnArray()
    {
        var stacks = (await GetProfileJsonAsync()).GetProperty("stacks");

        Assert.Equal(["JVM 17", "Docker"], stacks.EnumerateArray().Select(stack => stack.GetString()));
    }

    [Fact]
    public async Task GetProfileInfo_ReturnsExperiencesWithIsCurrentAndNullDateTo()
    {
        var experience = new ExperienceDto("Test Company Ltd.", "Developer", "B2B", "2023-01-01", null, true, "Description");
        profileService.GetProfileAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(BuildProfile(experiences: [experience]));

        var json = (await GetProfileJsonAsync()).GetProperty("experiences")[0];

        Assert.Equal("Test Company Ltd.", json.GetProperty("company").GetString());
        Assert.Equal("B2B", json.GetProperty("contractType").GetString());
        Assert.True(json.GetProperty("isCurrent").GetBoolean());
        Assert.Equal(JsonValueKind.Null, json.GetProperty("dateTo").ValueKind);
    }

    [Fact]
    public async Task GetProfileInfo_ReturnsAnEmptyExperienceArray()
    {
        Assert.Equal(0, (await GetProfileJsonAsync()).GetProperty("experiences").GetArrayLength());
    }

    [Fact]
    public async Task GetProfileInfo_PassesTheLangParameterToTheService()
    {
        await GetProfileJsonAsync("/api/profileinfo?lang=en");

        await profileService.Received(1).GetProfileAsync("en", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetProfileInfo_DefaultsToPl()
    {
        await GetProfileJsonAsync();

        await profileService.Received(1).GetProfileAsync("pl", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetHello_ReturnsPlainText()
    {
        var response = await client.GetAsync("/api/hello");

        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Hello World from C# + ASP.NET Core! 🚀", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task GetBackend_ReturnsTheLabelOfThisBackend()
    {
        var json = await client.GetFromJsonAsync<JsonElement>("/api/backend");

        Assert.Equal(ProfileController.BackendLabel, json.GetProperty("label").GetString());
        await profileService.DidNotReceiveWithAnyArgs().GetProfileAsync(default!, default);
    }
}

public sealed class PandaGameControllerTests : IDisposable
{
    private readonly IPandaGamePlayerScoreService pandaGameService = Substitute.For<IPandaGamePlayerScoreService>();
    private readonly CvApplicationFactory factory;
    private readonly HttpClient client;

    public PandaGameControllerTests()
    {
        factory = new CvApplicationFactory
        {
            ConfigureServices = services => services.Replace(ServiceDescriptor.Singleton(pandaGameService)),
        };
        client = factory.CreateClient();
    }

    public void Dispose() => factory.Dispose();

    private Task<HttpResponseMessage> PostScoreAsync(string json) =>
        client.PostAsync("/api/pandagame/scores", new StringContent(json, Encoding.UTF8, "application/json"));

    [Fact]
    public async Task PostScores_Returns201AndTheSavedScore()
    {
        pandaGameService.SaveScoreAsync(Arg.Any<PandaGamePlayerScoreRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PandaGamePlayerScoreResponse("Player1", 123, "2026-06-14T10:00:00"));

        var response = await PostScoreAsync("""{"nick":"Player1","score":123}""");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Player1", json.GetProperty("nick").GetString());
        Assert.Equal(123, json.GetProperty("score").GetInt32());
        await pandaGameService.Received(1).SaveScoreAsync(new PandaGamePlayerScoreRequest("Player1", 123), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PostScores_Returns400WithTheReasonWhenTheServiceRejectsTheData()
    {
        pandaGameService.SaveScoreAsync(Arg.Any<PandaGamePlayerScoreRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ArgumentException("Score cannot be negative"));

        var response = await PostScoreAsync("""{"nick":"Player1","score":-5}""");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Score cannot be negative", (await response.Content.ReadFromJsonAsync<ErrorResponse>())?.Error);
    }

    [Theory]
    [InlineData("""{"nick": "Player1", "score": }""")]
    [InlineData("""{"score": 10}""")]
    public async Task PostScores_Returns400WhenTheJsonIsMalformedOrIncomplete(string body)
    {
        var response = await PostScoreAsync(body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Invalid request data format", (await response.Content.ReadFromJsonAsync<ErrorResponse>())?.Error);
        await pandaGameService.DidNotReceiveWithAnyArgs().SaveScoreAsync(default!, default);
    }

    [Fact]
    public async Task PostScores_Returns500WhenTheServiceFailsUnexpectedly()
    {
        pandaGameService.SaveScoreAsync(Arg.Any<PandaGamePlayerScoreRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("DB down"));

        var response = await PostScoreAsync("""{"nick":"Player1","score":10}""");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("Internal server error", (await response.Content.ReadFromJsonAsync<ErrorResponse>())?.Error);
    }

    [Fact]
    public async Task GetScores_ReturnsTheList()
    {
        pandaGameService.TopScoresAsync(Arg.Any<CancellationToken>()).Returns(
        [
            new PandaGamePlayerScoreResponse("Player1", 200, "2026-06-14T10:00:00"),
            new PandaGamePlayerScoreResponse("Player2", 100, "2026-06-14T09:00:00"),
        ]);

        var json = await client.GetFromJsonAsync<JsonElement>("/api/pandagame/scores");

        Assert.Equal(["Player1", "Player2"], json.EnumerateArray().Select(score => score.GetProperty("nick").GetString()));
        Assert.Equal(200, json[0].GetProperty("score").GetInt32());
    }

    [Fact]
    public async Task GetScores_ReturnsAnEmptyArrayWhenThereAreNoScores()
    {
        pandaGameService.TopScoresAsync(Arg.Any<CancellationToken>()).Returns([]);

        Assert.Equal(0, (await client.GetFromJsonAsync<JsonElement>("/api/pandagame/scores")).GetArrayLength());
    }
}
