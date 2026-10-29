using Microsoft.EntityFrameworkCore;
using ServiceScheduler.Domain.Entities;

namespace ServiceScheduler.Infrastructure.Persistence;

public class SchedulerDbContext(DbContextOptions<SchedulerDbContext> options) : DbContext(options)
{
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<Dealership> Dealerships => Set<Dealership>();
    public DbSet<ServiceType> ServiceTypes => Set<ServiceType>();
    public DbSet<Technician> Technicians => Set<Technician>();
    public DbSet<ServiceBay> ServiceBays => Set<ServiceBay>();
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<Capability> Capabilities => Set<Capability>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SchedulerDbContext).Assembly);
}
