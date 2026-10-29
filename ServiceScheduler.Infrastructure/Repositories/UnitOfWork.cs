using Microsoft.EntityFrameworkCore;
using Npgsql;
using ServiceScheduler.Domain.Exceptions;
using ServiceScheduler.Domain.Interfaces;
using ServiceScheduler.Infrastructure.Persistence;
using ServiceScheduler.Infrastructure.Persistence.Configurations;

namespace ServiceScheduler.Infrastructure.Repositories;

internal sealed class UnitOfWork(SchedulerDbContext db) : IUnitOfWork
{
    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // One SaveChanges is one database transaction: everything staged commits or nothing does.
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (Translate(ex) is { } translated)
        {
            // Detach the rejected inserts so the same context can retry with another technician/bay or re-read the stored result.
            foreach (var entry in db.ChangeTracker.Entries().Where(e => e.State == EntityState.Added).ToList())
            {
                entry.State = EntityState.Detached;
            }

            throw translated;
        }
    }

    private static DomainException? Translate(Exception ex)
    {
        // Npgsql's execution strategy wraps transient errors (e.g. 40P01) in an InvalidOperationException,
        // so look through the whole chain instead of only DbUpdateException.InnerException.
        var postgres = FindPostgresError(ex);

        return postgres switch
        {
            // 23P01: the no-overlap constraint rejected the insert. 40P01: two concurrent overlapping inserts each
            // waited on the other's uncommitted row and PostgreSQL aborted this one. Both are a lost race, nothing persisted.
            { SqlState: PostgresErrorCodes.ExclusionViolation or PostgresErrorCodes.DeadlockDetected } =>
                new ResourceUnavailableException("The technician or service bay is no longer available for the requested time."),

            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: IdempotencyRecordConfiguration.KeyConstraintName } =>
                new DuplicateIdempotencyKeyException(),

            _ => null
        };
    }

    private static PostgresException? FindPostgresError(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current is PostgresException postgres)
            {
                return postgres;
            }
        }

        return null;
    }
}
