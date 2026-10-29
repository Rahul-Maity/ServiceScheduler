using ServiceScheduler.Domain.Entities;

namespace ServiceScheduler.Domain.Interfaces;

public interface ICustomerRepository
{
    Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface IVehicleRepository
{
    Task<Vehicle?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface IDealershipRepository
{
    /// <summary>Includes the dealership's working hours.</summary>
    Task<Dealership?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface IServiceTypeRepository
{
    /// <summary>Includes required skills and capabilities.</summary>
    Task<ServiceType?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
