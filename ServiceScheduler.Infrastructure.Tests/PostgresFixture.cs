using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ServiceScheduler.Infrastructure.Persistence;

namespace ServiceScheduler.Infrastructure.Tests;

/// <summary>
/// The local PostgreSQL, in a dedicated test database (never the dev one), with the schema created by the real migrations.
/// Host and credentials come from the Api's appsettings.Development.json; set SCHEDULER_TEST_CONNECTION to override entirely.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    public const string TestDatabase = "servicescheduler_tests";

    public string ConnectionString { get; } = ResolveConnectionString();

    public SchedulerDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<SchedulerDbContext>().UseNpgsql(ConnectionString).Options);

    public async Task InitializeAsync()
    {
        await using var db = CreateContext();
        await db.Database.MigrateAsync(); // also creates the test database if it does not exist yet
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static string ResolveConnectionString()
    {
        var overridden = Environment.GetEnvironmentVariable("SCHEDULER_TEST_CONNECTION");
        if (!string.IsNullOrWhiteSpace(overridden))
        {
            return overridden;
        }

        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "appsettings.Development.json")));
        var apiConnection = json.RootElement.GetProperty("ConnectionStrings").GetProperty("Scheduler").GetString()!;

        return new NpgsqlConnectionStringBuilder(apiConnection) { Database = TestDatabase }.ConnectionString;
    }
}
