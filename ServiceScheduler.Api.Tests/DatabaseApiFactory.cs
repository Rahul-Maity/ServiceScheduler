using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ServiceScheduler.Domain.Entities;
using ServiceScheduler.Domain.Interfaces;
using ServiceScheduler.Infrastructure.Persistence;

namespace ServiceScheduler.Api.Tests;

/// <summary>
/// The whole real stack (controllers, Application, Infrastructure) on the local PostgreSQL, in a database used only by
/// these tests. Call <see cref="ResetAsync"/> before each test: every table is emptied, the schema stays.
/// </summary>
public sealed class DatabaseApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string TestDatabase = "servicescheduler_api_tests";

    public string ConnectionString { get; } = ResolveConnectionString();

    /// <summary>When set, every booking's candidate selection waits until that many requests have all seen the slot as free.</summary>
    public SelectionGate Gate { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:Scheduler", ConnectionString);
        builder.ConfigureTestServices(services =>
        {
            var original = services.Single(d => d.ServiceType == typeof(IServiceBayRepository));
            services.Remove(original);
            services.AddScoped<IServiceBayRepository>(sp => new GatedServiceBayRepository(
                (IServiceBayRepository)ActivatorUtilities.CreateInstance(sp, original.ImplementationType!), Gate));
        });
    }

    async Task IAsyncLifetime.InitializeAsync()
    {
        await using var db = new SchedulerDbContext(new DbContextOptionsBuilder<SchedulerDbContext>().UseNpgsql(ConnectionString).Options);
        await db.Database.MigrateAsync(); // creates the test database on first use
    }

    Task IAsyncLifetime.DisposeAsync() => Task.CompletedTask;

    public async Task ResetAsync()
    {
        Gate.Reset();
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();

        var tables = new List<string>();
        await using (var list = new NpgsqlCommand(
                         "select tablename from pg_tables where schemaname = 'public' and tablename <> '__EFMigrationsHistory'", connection))
        await using (var reader = await list.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                tables.Add($"\"{reader.GetString(0)}\"");
            }
        }

        await using var truncate = new NpgsqlCommand($"TRUNCATE TABLE {string.Join(", ", tables)} CASCADE", connection);
        await truncate.ExecuteNonQueryAsync();
    }

    public async Task<T> WithDbAsync<T>(Func<SchedulerDbContext, Task<T>> action)
    {
        await using var scope = Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<SchedulerDbContext>());
    }

    private static string ResolveConnectionString()
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "appsettings.Development.json")));
        var apiConnection = json.RootElement.GetProperty("ConnectionStrings").GetProperty("Scheduler").GetString()!;
        return new NpgsqlConnectionStringBuilder(apiConnection) { Database = TestDatabase }.ConnectionString;
    }
}

/// <summary>Makes N concurrent requests all finish candidate selection before any of them commits.</summary>
public sealed class SelectionGate
{
    private int _size;
    private int _arrived;
    private TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>How many requests have finished candidate selection while the gate was armed.</summary>
    public int Arrived => _arrived;

    public void Require(int concurrentRequests)
    {
        Reset();
        _size = concurrentRequests;
    }

    public void Reset()
    {
        _size = 0;
        _arrived = 0;
        _released = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public async Task ArriveAsync()
    {
        if (_size == 0)
        {
            return;
        }

        if (Interlocked.Increment(ref _arrived) >= _size)
        {
            _released.TrySetResult();
        }

        await _released.Task.WaitAsync(TimeSpan.FromSeconds(30));
    }
}

/// <summary>Runs after the technician query, so by the time requests are released each one holds a stale "free" snapshot.</summary>
internal sealed class GatedServiceBayRepository(IServiceBayRepository inner, SelectionGate gate) : IServiceBayRepository
{
    public async Task<IReadOnlyList<ServiceBay>> FindAvailableAsync(
        Guid dealershipId, ServiceType serviceType, DateTimeOffset startAt, DateTimeOffset endAt, CancellationToken cancellationToken = default)
    {
        var bays = await inner.FindAvailableAsync(dealershipId, serviceType, startAt, endAt, cancellationToken);
        await gate.ArriveAsync();
        return bays;
    }
}
