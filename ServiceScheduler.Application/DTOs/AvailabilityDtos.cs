namespace ServiceScheduler.Application.DTOs;

/// <param name="StartAt">Any offset; normalized to UTC by the service.</param>
public sealed record AvailabilityRequest(Guid DealershipId, Guid ServiceTypeId, DateTimeOffset StartAt);

/// <summary>The requested interval as UTC [StartAt, EndAt) and whether a technician and bay are free for all of it.</summary>
public sealed record AvailabilityResult(bool IsAvailable, DateTimeOffset StartAt, DateTimeOffset EndAt);
