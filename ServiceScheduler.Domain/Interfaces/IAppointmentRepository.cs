using ServiceScheduler.Domain.Entities;

namespace ServiceScheduler.Domain.Interfaces;

public interface IAppointmentRepository
{
    Task<Appointment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Stages the appointment; it is persisted by <see cref="IUnitOfWork.SaveChangesAsync"/>.</summary>
    Task AddAsync(Appointment appointment, CancellationToken cancellationToken = default);
}
