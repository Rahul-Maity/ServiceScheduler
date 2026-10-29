using Microsoft.EntityFrameworkCore;
using ServiceScheduler.Domain.Entities;
using ServiceScheduler.Domain.Enums;
using ServiceScheduler.Domain.Interfaces;
using ServiceScheduler.Infrastructure.Persistence;

namespace ServiceScheduler.Infrastructure.Repositories;

internal sealed class TechnicianRepository(SchedulerDbContext db) : ITechnicianRepository
{
    public async Task<IReadOnlyList<Technician>> FindAvailableAsync(
        Guid dealershipId,
        ServiceType serviceType,
        DateTimeOffset startAt,
        DateTimeOffset endAt,
        CancellationToken cancellationToken = default)
    {
        var start = startAt.ToUniversalTime();
        var end = endAt.ToUniversalTime();
        var requiredSkillIds = serviceType.RequiredSkills.Select(s => s.Id).ToList();

        return await db.Technicians
            .AsNoTracking()
            .Include(t => t.Skills)
            .Where(t => t.DealershipId == dealershipId
                        && t.Skills.Count(s => requiredSkillIds.Contains(s.Id)) == requiredSkillIds.Count
                        && !db.Appointments.Any(a => a.TechnicianId == t.Id
                                                     && a.Status == AppointmentStatus.Confirmed
                                                     && a.StartAt < end
                                                     && start < a.EndAt))
            .OrderBy(t => t.Name).ThenBy(t => t.Id)
            .ToListAsync(cancellationToken);
    }
}
