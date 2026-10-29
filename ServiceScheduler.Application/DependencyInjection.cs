using Microsoft.Extensions.DependencyInjection;
using ServiceScheduler.Application.Interfaces;
using ServiceScheduler.Application.Services;

namespace ServiceScheduler.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ResourceSelector>();
        services.AddScoped<IAvailabilityService, AvailabilityService>();
        services.AddScoped<IBookingService, BookingService>();
        return services;
    }
}
