using Microsoft.EntityFrameworkCore;
using ServiceScheduler.Domain.Entities;
using ServiceScheduler.Domain.Interfaces;
using ServiceScheduler.Infrastructure.Persistence;

namespace ServiceScheduler.Infrastructure.Repositories;

internal sealed class CustomerRepository(SchedulerDbContext db) : ICustomerRepository
{
    public Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
}

internal sealed class VehicleRepository(SchedulerDbContext db) : IVehicleRepository
{
    public Task<Vehicle?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Vehicles.AsNoTracking().FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
}

internal sealed class DealershipRepository(SchedulerDbContext db) : IDealershipRepository
{
    public Task<Dealership?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Dealerships.AsNoTracking().Include(d => d.WorkingHours).FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
}

internal sealed class ServiceTypeRepository(SchedulerDbContext db) : IServiceTypeRepository
{
    public Task<ServiceType?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.ServiceTypes.AsNoTracking()
            .Include(s => s.RequiredSkills)
            .Include(s => s.RequiredCapabilities)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
}
