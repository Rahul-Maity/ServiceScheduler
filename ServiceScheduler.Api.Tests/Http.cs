using System.Net.Http.Json;

namespace ServiceScheduler.Api.Tests;

/// <summary>The wire format as a client sees it; deliberately independent of the Api's own contract types.</summary>
public sealed record AppointmentWire(
    Guid Id, Guid CustomerId, Guid VehicleId, Guid DealershipId, Guid ServiceTypeId, Guid TechnicianId, Guid ServiceBayId,
    DateTimeOffset StartAt, DateTimeOffset EndAt, string Status, DateTimeOffset CreatedAt, DateTimeOffset? CancelledAt);

public sealed record AvailabilityWire(bool IsAvailable, DateTimeOffset StartAt, DateTimeOffset EndAt);

public sealed record ProblemWire(int Status, string? Title, string? Detail, string? Code, string? CorrelationId);

public static class Http
{
    public static HttpRequestMessage Post(string url, object? body, string? idempotencyKey = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        if (idempotencyKey is not null)
        {
            request.Headers.Add("Idempotency-Key", idempotencyKey);
        }

        return request;
    }

    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>())!;

    public static async Task<ProblemWire> ReadProblemAsync(this HttpResponseMessage response)
    {
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        return await response.ReadAsync<ProblemWire>();
    }

    public static object AvailabilityBody(string? startAt) =>
        new { dealershipId = Guid.NewGuid(), serviceTypeId = Guid.NewGuid(), startAt };

    public static object BookingBody(string? startAt, Guid? customerId = null) => new
    {
        customerId = customerId ?? Guid.NewGuid(),
        vehicleId = Guid.NewGuid(),
        dealershipId = Guid.NewGuid(),
        serviceTypeId = Guid.NewGuid(),
        startAt
    };
}
