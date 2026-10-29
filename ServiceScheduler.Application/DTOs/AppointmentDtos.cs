namespace ServiceScheduler.Application.DTOs;

/// <param name="StartAt">Any offset; normalized to UTC by the service.</param>
public sealed record BookAppointmentRequest(
    Guid CustomerId,
    Guid VehicleId,
    Guid DealershipId,
    Guid ServiceTypeId,
    DateTimeOffset StartAt);

public sealed record AppointmentDto(
    Guid Id,
    Guid CustomerId,
    Guid VehicleId,
    Guid DealershipId,
    Guid ServiceTypeId,
    Guid TechnicianId,
    Guid ServiceBayId,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CancelledAt,
    /// <summary>True when this is the stored result of an earlier request with the same Idempotency-Key.</summary>
    bool IsReplay = false);
