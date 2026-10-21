using System.Reflection;
using System.Text.RegularExpressions;
using DbUp;
using DbUp.Engine;
using DbUp.Engine.Output;

namespace CvAspNet.Web.Data;

/// <summary>
/// Applies the SQL migrations shared with cv-spring-kotlin (cv-shared/db/migration, embedded in this assembly).
/// <para>
/// The scripts follow Flyway's naming, V&lt;version&gt;__&lt;description&gt;.sql, and run in version order.
/// The journal is DbUp's own table, so this backend needs its own database: a database migrated by
/// Flyway (the Kotlin backend) has no DbUp journal and the scripts would run on it again.
/// </para>
/// </summary>
public static partial class DatabaseMigrator
{
    public const string JournalTable = "dbup_schema_history";
    private const string ResourcePrefix = "db/migration/";

    [GeneratedRegex(@"^V(?<version>\d+)__.+\.sql$")]
    private static partial Regex FlywayScriptName();

    /// <summary>Creates the database when missing and runs the scripts it has not run yet.</summary>
    /// <exception cref="InvalidOperationException">A script failed; the scripts before it stay applied.</exception>
    public static void Migrate(string connectionString, ILogger logger)
    {
        EnsureDatabase.For.PostgresqlDatabase(connectionString, new NoOpUpgradeLog());

        var upgrader = DeployChanges.To
            .PostgresqlDatabase(connectionString)
            .WithScripts(LoadScripts())
            .JournalToPostgresqlTable("public", JournalTable)
            .WithTransactionPerScript()
            .WithVariablesDisabled()
            .LogToNowhere()
            .Build();

        var result = upgrader.PerformUpgrade();
        if (!result.Successful)
        {
            throw new InvalidOperationException($"Database migration failed at {result.ErrorScript?.Name}", result.Error);
        }

        foreach (var script in result.Scripts)
        {
            logger.LogInformation("Applied migration {Script}", script.Name);
        }
        logger.LogInformation("Database schema is up to date ({Count} new migrations)", result.Scripts.Count());
    }

    /// <summary>The embedded scripts in version order (V2 before V10, unlike a plain name sort).</summary>
    internal static IReadOnlyList<SqlScript> LoadScripts()
    {
        var assembly = typeof(DatabaseMigrator).Assembly;
        return assembly.GetManifestResourceNames()
            .Where(resource => resource.StartsWith(ResourcePrefix, StringComparison.Ordinal))
            .Select(resource => (Name: resource[ResourcePrefix.Length..], Resource: resource))
            .Select(script => (script.Name, script.Resource, Version: ParseVersion(script.Name)))
            .OrderBy(script => script.Version)
            .Select(script => new SqlScript(
                script.Name,
                ReadResource(assembly, script.Resource),
                new SqlScriptOptions { RunGroupOrder = script.Version }))
            .ToList();
    }

    private static int ParseVersion(string scriptName)
    {
        var match = FlywayScriptName().Match(scriptName);
        if (!match.Success)
        {
            throw new InvalidOperationException($"Migration {scriptName} does not follow V<version>__<description>.sql");
        }
        return int.Parse(match.Groups["version"].Value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string ReadResource(Assembly assembly, string resource)
    {
        using var stream = assembly.GetManifestResourceStream(resource)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
