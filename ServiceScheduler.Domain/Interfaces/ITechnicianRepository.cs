using ServiceScheduler.Domain.Entities;

namespace ServiceScheduler.Domain.Interfaces;

public interface ITechnicianRepository
{
    /// <summary>
    /// Technicians of the dealership who are qualified for the service type and have no Confirmed
    /// appointment overlapping [startAt, endAt). Read-only; reserves nothing.
    /// </summary>
    Task<IReadOnlyList<Technician>> FindAvailableAsync(
        Guid dealershipId,
        ServiceType serviceType,
        DateTimeOffset startAt,
        DateTimeOffset endAt,
        CancellationToken cancellationToken = default);
}
