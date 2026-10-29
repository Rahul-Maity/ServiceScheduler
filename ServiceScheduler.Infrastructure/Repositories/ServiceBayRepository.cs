using Microsoft.EntityFrameworkCore;
using ServiceScheduler.Domain.Entities;
using ServiceScheduler.Domain.Enums;
using ServiceScheduler.Domain.Interfaces;
using ServiceScheduler.Infrastructure.Persistence;

namespace ServiceScheduler.Infrastructure.Repositories;

internal sealed class ServiceBayRepository(SchedulerDbContext db) : IServiceBayRepository
{
    public async Task<IReadOnlyList<ServiceBay>> FindAvailableAsync(
        Guid dealershipId,
        ServiceType serviceType,
        DateTimeOffset startAt,
        DateTimeOffset endAt,
        CancellationToken cancellationToken = default)
    {
        var start = startAt.ToUniversalTime();
        var end = endAt.ToUniversalTime();
        var requiredCapabilityIds = serviceType.RequiredCapabilities.Select(c => c.Id).ToList();

        return await db.ServiceBays
            .AsNoTracking()
            .Include(b => b.Capabilities)
            .Where(b => b.DealershipId == dealershipId
                        && b.Capabilities.Count(c => requiredCapabilityIds.Contains(c.Id)) == requiredCapabilityIds.Count
                        && !db.Appointments.Any(a => a.ServiceBayId == b.Id
                                                     && a.Status == AppointmentStatus.Confirmed
                                                     && a.StartAt < end
                                                     && start < a.EndAt))
            .OrderBy(b => b.Name).ThenBy(b => b.Id)
            .ToListAsync(cancellationToken);
    }
}
