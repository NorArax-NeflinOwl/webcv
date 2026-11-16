using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CvAspNet.Web.Data;
using CvAspNet.Web.Models;
using CvAspNet.Web.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CvAspNet.Tests.Web;

/// <summary>The whole application on SQLite: pages, health, the real services and repositories.</summary>
public sealed class ApplicationTests : IDisposable
{
    private readonly CvApplicationFactory factory = new();
    private readonly HttpClient client;

    public ApplicationTests() => client = factory.CreateClient();

    public void Dispose() => factory.Dispose();

    [Theory]
    [InlineData("/", "<script src=\"/js/index.js\"></script>")]
    [InlineData("/pandaGame", "/js/pandaGame.js")]
    [InlineData("/pandaGame.html", "/js/pandaGame.js")]
    [InlineData("/pokerGame", "/js/pokerGame.js")]
    [InlineData("/js/index.js", "/api/backend")]
    public async Task SharedPages_AreServed(string path, string expectedContent)
    {
        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(expectedContent, await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/js/index.js")]
    [InlineData("/css/indexStyle.css")]
    public async Task PageFiles_AreRevalidatedOnEveryLoad(string path)
    {
        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoCache, $"{path} must carry Cache-Control: no-cache");
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/pokerGame")]
    [InlineData("/api/pandagame/scores")]
    public async Task Responses_CarryTheSecurityHeaders(string path)
    {
        var response = await client.GetAsync(path);

        var csp = string.Join(";", response.Headers.GetValues("Content-Security-Policy"));
        Assert.Contains("script-src 'self';", csp);
        Assert.Contains("frame-ancestors 'none'", csp);
        Assert.Contains("connect-src 'self'", csp);
        Assert.Equal("DENY", Assert.Single(response.Headers.GetValues("X-Frame-Options")));
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/pandaGame.html")]
    [InlineData("/pokerGame.html")]
    public async Task SharedPages_HaveNothingInlineThatTheCspWouldBlock(string path)
    {
        var html = await client.GetStringAsync(path);

        Assert.DoesNotContain("style=", html);
        Assert.DoesNotContain("<style", html);
        Assert.Matches("^(?s)((?!<script>).)*$", html);  // every script has a src
        Assert.DoesNotMatch(@"\son[a-z]+=", html);       // no inline event handlers
    }

    [Fact]
    public async Task ActuatorHealth_ReportsUpLikeSpringBoot()
    {
        var json = await client.GetFromJsonAsync<JsonElement>("/actuator/health");

        Assert.Equal("UP", json.GetProperty("status").GetString());
    }

    [Fact]
    public async Task PandaScores_AreSavedAndListedThroughTheRealStack()
    {
        var saved = await client.PostAsJsonAsync("/api/pandagame/scores", new PandaGamePlayerScoreRequest("  Panda  ", 42));
        Assert.Equal(HttpStatusCode.Created, saved.StatusCode);

        var scores = await client.GetFromJsonAsync<List<PandaGamePlayerScoreResponse>>("/api/pandagame/scores");

        var score = Assert.Single(scores!);
        Assert.Equal("Panda", score.Nick);
        Assert.Equal(42, score.Score);
    }

    [Fact]
    public async Task ProfileInfo_IsReadThroughTheRealStack()
    {
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CvDbContext>();
            var profile = new Profile { Name = "Jan Kowalski", FooterStatus = FooterStatus.Green, Email = "jan@example.com" };
            db.Profiles.Add(profile);
            db.ProfileTranslations.Add(new ProfileTranslation { ProfileId = profile.Id, Backend = ProfileService.Backend, Language = "pl", Bio = "Bio PL" });
            db.ProfileTranslations.Add(new ProfileTranslation { ProfileId = profile.Id, Backend = "kotlin", Language = "pl", Bio = "Bio Kotlin" });
            await db.SaveChangesAsync();
        }

        var json = await client.GetFromJsonAsync<JsonElement>("/api/profileinfo?lang=en");

        Assert.Equal("Bio PL", json.GetProperty("bio").GetString());  // no "en" translation: falls back to "pl", of this backend
        Assert.Equal("GREEN", json.GetProperty("footerStatus").GetString());
        Assert.Equal("jan@example.com", json.GetProperty("links").GetProperty("E-mail").GetString());
    }

    [Fact]
    public async Task ProfileInfo_FailsWhenTheDatabaseHasNoProfile()
    {
        var response = await client.GetAsync("/api/profileinfo");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }
}

public class DatabaseMigratorTests
{
    [Fact]
    public void Scripts_AreTheSharedMigrationsInVersionOrder()
    {
        var scripts = DatabaseMigrator.LoadScripts();

        Assert.Equal(
            ["V1__init.sql", "V2__seed.sql", "V3__update_skills_and_stack.sql", "V4__add_translations.sql", "V5__split_profile_by_backend.sql", "V6__real_experience.sql"],
            scripts.Select(script => script.Name));
        Assert.Equal([1, 2, 3, 4, 5, 6], scripts.Select(script => script.SqlScriptOptions.RunGroupOrder));
        Assert.Contains("CREATE TABLE profile", scripts[0].Contents);
    }
}
