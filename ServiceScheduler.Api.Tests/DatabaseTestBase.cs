using Microsoft.EntityFrameworkCore;
using ServiceScheduler.Application.Interfaces;
using ServiceScheduler.Domain.Enums;

namespace ServiceScheduler.Api.Tests;

[CollectionDefinition("Database")]
public sealed class DatabaseCollection : ICollectionFixture<DatabaseApiFactory>;

/// <summary>Every test starts from empty tables; classes in the collection never run in parallel with each other.</summary>
public abstract class DatabaseTestBase(DatabaseApiFactory factory) : IAsyncLifetime
{
    protected DatabaseApiFactory Factory { get; } = factory;
    protected HttpClient Client { get; } = factory.CreateClient();

    public Task InitializeAsync() => Factory.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    protected static string NewKey() => Guid.NewGuid().ToString();

    protected Task<HttpResponseMessage> PostAsync(string url, object? body, string? key = null) =>
        Client.SendAsync(Http.Post(url, body, key));

    protected Task<int> AppointmentCountAsync(AppointmentStatus? status = null) =>
        Factory.WithDbAsync(db => db.Appointments.CountAsync(a => status == null || a.Status == status));

    protected Task<int> RecordCountAsync() =>
        Factory.WithDbAsync(db => db.Set<IdempotencyRecord>().CountAsync());
}
