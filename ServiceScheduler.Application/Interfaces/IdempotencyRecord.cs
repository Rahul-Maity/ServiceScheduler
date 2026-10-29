namespace ServiceScheduler.Application.Interfaces;

/// <summary>What is remembered for an Idempotency-Key: the request fingerprint and the successful response.</summary>
public sealed class IdempotencyRecord(string key, string requestHash, string responseJson, DateTimeOffset createdAt)
{
    public string Key { get; } = key;
    public string RequestHash { get; } = requestHash;
    public string ResponseJson { get; } = responseJson;
    public DateTimeOffset CreatedAt { get; } = createdAt;
}
