namespace ServiceScheduler.Domain.Interfaces;

public interface IUnitOfWork
{
    /// <summary>
    /// Persists all pending changes in a single transaction (e.g. appointment creation, or cancellation).
    /// Infrastructure translates exclusion-constraint violations into <see cref="Exceptions.ResourceUnavailableException"/>.
    /// </summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
