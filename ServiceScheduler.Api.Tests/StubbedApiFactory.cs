using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ServiceScheduler.Application.Interfaces;

namespace ServiceScheduler.Api.Tests;

/// <summary>The real Api pipeline with the Application services replaced by stubs (HTTP contract tests; no database is touched).</summary>
public sealed class StubbedApiFactory : WebApplicationFactory<Program>
{
    public StubBookingService Booking { get; } = new();
    public StubAvailabilityService Availability { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IBookingService>();
            services.RemoveAll<IAvailabilityService>();
            services.AddSingleton<IBookingService>(Booking);
            services.AddSingleton<IAvailabilityService>(Availability);
        });
    }
}
