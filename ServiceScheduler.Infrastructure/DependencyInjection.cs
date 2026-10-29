using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceScheduler.Application.Interfaces;
using ServiceScheduler.Domain.Interfaces;
using ServiceScheduler.Infrastructure.Persistence;
using ServiceScheduler.Infrastructure.Repositories;
using ServiceScheduler.Infrastructure.Services;

namespace ServiceScheduler.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Registers the DbContext, repositories and Unit of Work. The connection string comes from Api configuration.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<SchedulerDbContext>(options => options.UseNpgsql(connectionString));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IAppointmentRepository, AppointmentRepository>();
        services.AddScoped<ITechnicianRepository, TechnicianRepository>();
        services.AddScoped<IServiceBayRepository, ServiceBayRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IVehicleRepository, VehicleRepository>();
        services.AddScoped<IDealershipRepository, DealershipRepository>();
        services.AddScoped<IServiceTypeRepository, ServiceTypeRepository>();
        services.AddScoped<IIdempotencyStore, IdempotencyStore>();
        services.AddSingleton<IClock, SystemClock>();
        return services;
    }
}
