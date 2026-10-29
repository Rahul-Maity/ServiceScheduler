using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ServiceScheduler.Infrastructure.Persistence;

/// <summary>
/// Used only by `dotnet ef` to build the model. The connection string comes from the environment;
/// the fallback has no credentials and is never used to connect when adding migrations.
/// </summary>
internal sealed class SchedulerDbContextFactory : IDesignTimeDbContextFactory<SchedulerDbContext>
{
    public SchedulerDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Scheduler")
                               ?? "Host=localhost;Database=servicescheduler";
        var options = new DbContextOptionsBuilder<SchedulerDbContext>().UseNpgsql(connectionString).Options;
        return new SchedulerDbContext(options);
    }
}
