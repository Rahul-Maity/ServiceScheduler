namespace ServiceScheduler.Application.Interfaces;

public interface IIdempotencyStore
{
    Task<IdempotencyRecord?> FindAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stages the record; it is persisted by the same <c>IUnitOfWork.SaveChangesAsync</c> commit as the appointment.
    /// A concurrent commit of the same key surfaces as <c>DuplicateIdempotencyKeyException</c>.
    /// </summary>
    Task AddAsync(IdempotencyRecord record, CancellationToken cancellationToken = default);
}
