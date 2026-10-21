using CvAspNet.Web.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CvAspNet.Tests.Web;

/// <summary>
/// An in-memory SQLite database with the model's tables — the counterpart of the Kotlin tests' H2
/// (create-drop, no migrations). Lives as long as the open connection.
/// </summary>
public sealed class TestDatabase : IDisposable
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");

    public TestDatabase()
    {
        connection.Open();
        using var db = CreateContext();
        db.Database.EnsureCreated();
    }

    public CvDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CvDbContext>();
        Configure(options);
        return new CvDbContext(options.Options);
    }

    public DbContextOptionsBuilder Configure(DbContextOptionsBuilder options) =>
        options.UseSqlite(connection).UseSnakeCaseNamingConvention();

    public void Dispose() => connection.Dispose();
}

/// <summary>
/// The real application on a test server, with SQLite in place of PostgreSQL and without the startup migration.
/// </summary>
public sealed class CvApplicationFactory : WebApplicationFactory<Program>
{
    private readonly TestDatabase database = new();

    /// <summary>Replaces services before the host starts, e.g. a substitute service.</summary>
    public Action<IServiceCollection> ConfigureServices { get; init; } = _ => { };

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Database:MigrateOnStartup", "false");
        builder.UseSetting("ConnectionStrings:CvDatabase", "Host=unused;Database=unused");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<CvDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<CvDbContext>>();
            services.AddDbContext<CvDbContext>(options => database.Configure(options));
            ConfigureServices(services);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) database.Dispose();
    }
}
