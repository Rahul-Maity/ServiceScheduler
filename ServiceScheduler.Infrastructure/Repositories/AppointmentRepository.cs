using Microsoft.EntityFrameworkCore;
using ServiceScheduler.Domain.Entities;
using ServiceScheduler.Domain.Interfaces;
using ServiceScheduler.Infrastructure.Persistence;

namespace ServiceScheduler.Infrastructure.Repositories;

internal sealed class AppointmentRepository(SchedulerDbContext db) : IAppointmentRepository
{
    public Task<Appointment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Appointments.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public async Task AddAsync(Appointment appointment, CancellationToken cancellationToken = default) =>
        await db.Appointments.AddAsync(appointment, cancellationToken);
}
