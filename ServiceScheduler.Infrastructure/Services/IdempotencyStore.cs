using Microsoft.EntityFrameworkCore;
using ServiceScheduler.Application.Interfaces;
using ServiceScheduler.Infrastructure.Persistence;

namespace ServiceScheduler.Infrastructure.Services;

internal sealed class IdempotencyStore(SchedulerDbContext db) : IIdempotencyStore
{
    public Task<IdempotencyRecord?> FindAsync(string key, CancellationToken cancellationToken = default) =>
        db.Set<IdempotencyRecord>().AsNoTracking().FirstOrDefaultAsync(r => r.Key == key, cancellationToken);

    public async Task AddAsync(IdempotencyRecord record, CancellationToken cancellationToken = default) =>
        await db.Set<IdempotencyRecord>().AddAsync(record, cancellationToken);
}
