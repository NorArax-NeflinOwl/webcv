using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CvAspNet.Web.Models;
using CvAspNet.Web.Poker;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace CvAspNet.Tests.Web;

/// <summary>Per-visitor limits, with the visitor's address taken from the gateway's X-Forwarded-For.</summary>
public sealed class RateLimitTests : IDisposable
{
    private const string Gateway = "192.168.1.7";

    private readonly List<CvApplicationFactory> factories = [];

    public void Dispose() => factories.ForEach(factory => factory.Dispose());

    /// <summary>Every request arrives from <paramref name="remoteAddress"/>, like the gateway's connections.</summary>
    private HttpClient Client(string remoteAddress, Dictionary<string, string> settings)
    {
        var factory = new CvApplicationFactory
        {
            Settings = settings,
            ConfigureServices = services => services.AddSingleton<IStartupFilter>(new RemoteAddressFilter(IPAddress.Parse(remoteAddress))),
        };
        factories.Add(factory);
        return factory.CreateClient();
    }

    private sealed class RemoteAddressFilter(IPAddress address) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, nextMiddleware) =>
            {
                context.Connection.RemoteIpAddress = address;
                return nextMiddleware(context);
            });
            next(app);
        };
    }

    private static Task<HttpResponseMessage> CreateTableAs(HttpClient client, string visitor)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/pokertable") { Content = JsonContent.Create(new CreateTableRequest(10)) };
        request.Headers.Add("X-Forwarded-For", visitor);
        return client.SendAsync(request);
    }

    [Fact]
    public async Task CreateTable_Is429BeyondTheLimitOfOneVisitorButNotForAnother()
    {
        var client = Client(Gateway, new()
        {
            ["ForwardedHeaders:KnownProxies"] = Gateway,
            ["RateLimits:PokerTablesPerMinute"] = "2",
        });

        Assert.Equal(HttpStatusCode.Created, (await CreateTableAs(client, "203.0.113.1")).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await CreateTableAs(client, "203.0.113.1")).StatusCode);
        var limited = await CreateTableAs(client, "203.0.113.1");

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.True(limited.Headers.Contains("Retry-After"));
        Assert.Contains("Too many requests", (await limited.Content.ReadFromJsonAsync<ErrorResponse>())!.Error);
        Assert.Equal(HttpStatusCode.Created, (await CreateTableAs(client, "203.0.113.2")).StatusCode);
    }

    [Fact]
    public async Task ForwardedFor_FromAnUnknownProxyIsIgnored()
    {
        // No proxy configured: a visitor cannot dodge the limit by inventing X-Forwarded-For addresses.
        var client = Client("198.51.100.9", new() { ["RateLimits:PokerTablesPerMinute"] = "1" });

        Assert.Equal(HttpStatusCode.Created, (await CreateTableAs(client, "203.0.113.1")).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await CreateTableAs(client, "203.0.113.2")).StatusCode);
    }

    [Fact]
    public async Task Join_IsLimitedPerVisitor()
    {
        var client = Client("198.51.100.9", new() { ["RateLimits:PokerJoinsPerMinute"] = "1" });
        var tableId = (await (await client.PostAsJsonAsync("/api/pokertable", new CreateTableRequest(10))).Content.ReadFromJsonAsync<CreateTableResponse>())!.TableId;

        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync($"/api/pokertable/{tableId}/join", new JoinTableRequest("Alice"))).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsJsonAsync($"/api/pokertable/{tableId}/join", new JoinTableRequest("Bob"))).StatusCode);
    }

    [Fact]
    public async Task PandaScore_IsLimitedPerVisitorButTheListIsNot()
    {
        var client = Client("198.51.100.9", new() { ["RateLimits:PandaScoresPerMinute"] = "1" });

        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/pandagame/scores", new PandaGamePlayerScoreRequest("Panda", 10))).StatusCode);
        var limited = await client.PostAsJsonAsync("/api/pandagame/scores", new PandaGamePlayerScoreRequest("Panda", 20));

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal(JsonValueKind.Array, (await client.GetFromJsonAsync<JsonElement>("/api/pandagame/scores")).ValueKind);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/pandagame/scores")).StatusCode);
    }
}
