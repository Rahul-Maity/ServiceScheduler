using ServiceScheduler.Domain.Entities;
using ServiceScheduler.Domain.Interfaces;

namespace ServiceScheduler.Application.Services;

/// <summary>Picks technician + bay combinations that are qualified, compatible and free for the whole interval.</summary>
public sealed class ResourceSelector(ITechnicianRepository technicians, IServiceBayRepository bays)
{
    /// <summary>Candidate pairs in preference order; later pairs are alternatives to retry after a conflict.</summary>
    public async Task<IReadOnlyList<(Technician Technician, ServiceBay Bay)>> FindPairsAsync(
        Dealership dealership,
        ServiceType serviceType,
        DateTimeOffset startAt,
        DateTimeOffset endAt,
        CancellationToken cancellationToken = default)
    {
        var freeTechnicians = await technicians.FindAvailableAsync(dealership.Id, serviceType, startAt, endAt, cancellationToken);
        if (freeTechnicians.Count == 0)
        {
            return [];
        }

        var freeBays = await bays.FindAvailableAsync(dealership.Id, serviceType, startAt, endAt, cancellationToken);

        return freeTechnicians.SelectMany(t => freeBays.Select(b => (t, b))).ToList();
    }
}
