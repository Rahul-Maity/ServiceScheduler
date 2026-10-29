namespace ServiceScheduler.Api.Contracts;

/// <param name="StartAt">ISO 8601 with a UTC offset, e.g. 2030-01-07T15:30:00+05:30.</param>
public sealed record CheckAvailabilityRequest(Guid DealershipId, Guid ServiceTypeId, string? StartAt);

/// <param name="StartAt">ISO 8601 with a UTC offset, e.g. 2030-01-07T15:30:00+05:30.</param>
public sealed record CreateAppointmentRequest(Guid CustomerId, Guid VehicleId, Guid DealershipId, Guid ServiceTypeId, string? StartAt);
