using ServiceScheduler.Application.DTOs;

namespace ServiceScheduler.Api.Contracts;

/// <param name="StartAt">UTC.</param>
/// <param name="EndAt">UTC; start plus the service duration.</param>
public sealed record AvailabilityResponse(bool IsAvailable, DateTimeOffset StartAt, DateTimeOffset EndAt);

/// <param name="Status">Confirmed or Cancelled.</param>
public sealed record AppointmentResponse(
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
    DateTimeOffset? CancelledAt);

internal static class ResponseMapping
{
    public static AvailabilityResponse ToResponse(this AvailabilityResult result) =>
        new(result.IsAvailable, result.StartAt, result.EndAt);

    public static AppointmentResponse ToResponse(this AppointmentDto a) => new(
        a.Id, a.CustomerId, a.VehicleId, a.DealershipId, a.ServiceTypeId, a.TechnicianId, a.ServiceBayId,
        a.StartAt, a.EndAt, a.Status, a.CreatedAt, a.CancelledAt);
}
