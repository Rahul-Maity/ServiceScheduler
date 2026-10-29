using ServiceScheduler.Domain.Entities;

namespace ServiceScheduler.Domain.Interfaces;

public interface IServiceBayRepository
{
    /// <summary>
    /// Bays of the dealership that support the service type's capabilities and have no Confirmed
    /// appointment overlapping [startAt, endAt). Read-only; reserves nothing.
    /// </summary>
    Task<IReadOnlyList<ServiceBay>> FindAvailableAsync(
        Guid dealershipId,
        ServiceType serviceType,
        DateTimeOffset startAt,
        DateTimeOffset endAt,
        CancellationToken cancellationToken = default);
}
