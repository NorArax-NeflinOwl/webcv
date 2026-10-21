using System.Text.Json;
using CvAspNet.Web.Data;
using CvAspNet.Web.Models;
using CvAspNet.Web.Poker;
using CvAspNet.Web.Repositories;
using CvAspNet.Web.Services;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Rewrite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.FileProviders;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

var connectionString = BuildConnectionString(builder.Configuration);
builder.Services.AddDbContext<CvDbContext>(options => options
    .UseNpgsql(connectionString, npgsql => npgsql.MapEnum<FooterStatus>(CvDbContext.FooterStatusEnumName))
    .UseSnakeCaseNamingConvention());

builder.Services.AddScoped<IProfileRepository, ProfileRepository>();
builder.Services.AddScoped<IProfileTranslationRepository, ProfileTranslationRepository>();
builder.Services.AddScoped<IProfileSkillRepository, ProfileSkillRepository>();
builder.Services.AddScoped<IProfileStackRepository, ProfileStackRepository>();
builder.Services.AddScoped<IExperienceRepository, ExperienceRepository>();
builder.Services.AddScoped<IExperienceTranslationRepository, ExperienceTranslationRepository>();
builder.Services.AddScoped<IPandaGamePlayerScoreRepository, PandaGamePlayerScoreRepository>();
builder.Services.AddScoped<IProfileService, ProfileService>();
builder.Services.AddScoped<IPandaGamePlayerScoreService, PandaGamePlayerScoreService>();
builder.Services.AddSingleton(TimeProvider.System);

// Poker: the tables and sessions live in memory, for the lifetime of the process.
builder.Services.AddSingleton<TableRepository>();
builder.Services.AddSingleton<SessionRegistry>();
builder.Services.AddSingleton<PokerWebSocketHandler>();

// A body that is not valid JSON (or misses a field) gets the same answer as from the Kotlin backend.
builder.Services.AddControllers().ConfigureApiBehaviorOptions(options =>
    options.InvalidModelStateResponseFactory = _ => new BadRequestObjectResult(new ErrorResponse("Invalid request data format")));

builder.Services.AddHealthChecks().AddDbContextCheck<CvDbContext>("database");

var app = builder.Build();

if (app.Configuration.GetValue("Database:MigrateOnStartup", true))
{
    DatabaseMigrator.Migrate(connectionString, app.Logger);
}

// The pages are cv-shared/web, copied next to the assembly; /pandaGame and /pokerGame are kept as aliases.
var webFiles = new PhysicalFileProvider(Path.Combine(AppContext.BaseDirectory, "web"));
app.UseRewriter(new RewriteOptions()
    .AddRewrite("^pandaGame$", "pandaGame.html", skipRemainingRules: true)
    .AddRewrite("^pokerGame$", "pokerGame.html", skipRemainingRules: true));
app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = webFiles });
app.UseStaticFiles(new StaticFileOptions { FileProvider = webFiles });

app.UseWebSockets();
app.Map("/api/pokertable/{id}/ws", (HttpContext context, string id, PokerWebSocketHandler handler) => handler.HandleAsync(context, id));

app.MapControllers();

// The address and the answer the page's health link expects from Spring Boot Actuator: {"status":"UP"}.
app.MapHealthChecks("/actuator/health", new HealthCheckOptions
{
    ResponseWriter = (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var status = report.Status == HealthStatus.Healthy ? "UP" : "DOWN";
        return context.Response.WriteAsync(JsonSerializer.Serialize(new { status }));
    },
});

app.Run();

// The connection string comes without the password, which is passed separately (Database:Password),
// so that it can stay out of appsettings.json.
static string BuildConnectionString(IConfiguration configuration)
{
    var connection = new NpgsqlConnectionStringBuilder(
        configuration.GetConnectionString("CvDatabase") ?? throw new InvalidOperationException("ConnectionStrings:CvDatabase is not set"))
    {
        // No Kerberos here: without this, every connection first looks for libgssapi, which the image lacks.
        GssEncryptionMode = GssEncryptionMode.Disable,
    };
    if (configuration["Database:Password"] is { Length: > 0 } password)
    {
        connection.Password = password;
    }
    return connection.ConnectionString;
}

public partial class Program;
