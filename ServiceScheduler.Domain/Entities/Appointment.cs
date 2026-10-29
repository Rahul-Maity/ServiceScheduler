using ServiceScheduler.Domain.Enums;
using ServiceScheduler.Domain.Exceptions;

namespace ServiceScheduler.Domain.Entities;

/// <summary>
/// A booking of one technician and one service bay for the half-open UTC interval [StartAt, EndAt).
/// Only <see cref="AppointmentStatus.Confirmed"/> appointments reserve resources.
/// </summary>
public sealed class Appointment
{
    private Appointment()
    {
    }

    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid VehicleId { get; private set; }
    public Guid DealershipId { get; private set; }
    public Guid ServiceTypeId { get; private set; }
    public Guid TechnicianId { get; private set; }
    public Guid ServiceBayId { get; private set; }
    public DateTimeOffset StartAt { get; private set; }
    public DateTimeOffset EndAt { get; private set; }
    public AppointmentStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }

    /// <summary>True while the appointment holds its technician and bay.</summary>
    public bool ReservesResources => Status == AppointmentStatus.Confirmed;

    /// <summary>
    /// Creates a confirmed appointment after validating the full set of business rules.
    /// <paramref name="startAt"/> may carry any offset; it is normalized to UTC. EndAt is computed here.
    /// Whether the resources are actually free is enforced by the database, not here.
    /// </summary>
    public static Appointment Book(
        Guid id,
        Customer customer,
        Vehicle vehicle,
        Dealership dealership,
        ServiceType serviceType,
        Technician technician,
        ServiceBay serviceBay,
        DateTimeOffset startAt,
        DateTimeOffset now)
    {
        Guard.NotEmpty(id, nameof(id));

        if (vehicle.CustomerId != customer.Id)
        {
            throw new DomainValidationException("The vehicle does not belong to the customer.");
        }

        if (technician.DealershipId != dealership.Id || serviceBay.DealershipId != dealership.Id)
        {
            throw new DomainValidationException("Technician and service bay must belong to the requested dealership.");
        }

        if (!technician.IsQualifiedFor(serviceType))
        {
            throw new DomainValidationException("The technician does not have the skills required for this service.");
        }

        if (!serviceBay.IsCompatibleWith(serviceType))
        {
            throw new DomainValidationException("The service bay does not support the capabilities required for this service.");
        }

        var start = startAt.ToUniversalTime();
        var end = start + serviceType.Duration;

        if (!dealership.IsOpenDuring(start, end))
        {
            throw new DomainValidationException("The requested time is outside the dealership's working hours.");
        }

        return new Appointment
        {
            Id = id,
            CustomerId = customer.Id,
            VehicleId = vehicle.Id,
            DealershipId = dealership.Id,
            ServiceTypeId = serviceType.Id,
            TechnicianId = technician.Id,
            ServiceBayId = serviceBay.Id,
            StartAt = start,
            EndAt = end,
            Status = AppointmentStatus.Confirmed,
            CreatedAt = now.ToUniversalTime()
        };
    }

    /// <summary>Confirmed -> Cancelled. Releases the technician and bay.</summary>
    public void Cancel(DateTimeOffset now)
    {
        if (Status != AppointmentStatus.Confirmed)
        {
            throw new InvalidStateTransitionException(Status.ToString(), nameof(AppointmentStatus.Cancelled));
        }

        Status = AppointmentStatus.Cancelled;
        CancelledAt = now.ToUniversalTime();
    }

    /// <summary>Half-open overlap: touching intervals (one ends where the other starts) do not overlap.</summary>
    public bool Overlaps(DateTimeOffset startAt, DateTimeOffset endAt) =>
        StartAt < endAt && startAt < EndAt;
}
