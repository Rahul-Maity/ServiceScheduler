using ServiceScheduler.Domain.Entities;
using ServiceScheduler.Infrastructure.Persistence;

namespace ServiceScheduler.Infrastructure.Tests;

/// <summary>
/// A freshly seeded, isolated dealership (own technicians, bays, customer) so tests can share one database.
/// Working hours are 08:00-18:00 UTC every day; <see cref="Day"/> is a Monday.
/// </summary>
public sealed class TestWorld
{
    public static readonly DateTimeOffset Day = new(2030, 1, 7, 0, 0, 0, TimeSpan.Zero);

    public required Dealership Dealership { get; init; }
    public required Customer Customer { get; init; }
    public required Vehicle Vehicle { get; init; }
    public required Skill Skill { get; init; }
    public required Capability Capability { get; init; }
    public required ServiceType OneHourService { get; init; }
    public required Technician Technician1 { get; init; }
    public required Technician Technician2 { get; init; }
    public required ServiceBay Bay1 { get; init; }
    public required ServiceBay Bay2 { get; init; }

    public static DateTimeOffset At(int hour, int minute = 0) => Day.AddHours(hour).AddMinutes(minute);

    public static async Task<TestWorld> SeedAsync(SchedulerDbContext db)
    {
        var dealership = new Dealership(Guid.NewGuid(), "Dealer", "UTC");
        foreach (var day in Enum.GetValues<DayOfWeek>())
        {
            dealership.SetWorkingHours(day, new TimeOnly(8, 0), new TimeOnly(18, 0));
        }

        var skill = new Skill(Guid.NewGuid(), $"Brakes-{Guid.NewGuid():N}");
        var capability = new Capability(Guid.NewGuid(), $"Lift-{Guid.NewGuid():N}");
        var customer = new Customer(Guid.NewGuid(), "Jane", "jane@example.com");
        var world = new TestWorld
        {
            Dealership = dealership,
            Customer = customer,
            Vehicle = new Vehicle(Guid.NewGuid(), customer.Id, Guid.NewGuid().ToString("N"), "Make", "Model"),
            Skill = skill,
            Capability = capability,
            OneHourService = new ServiceType(Guid.NewGuid(), "Brake service", TimeSpan.FromHours(1), [skill], [capability]),
            Technician1 = new Technician(Guid.NewGuid(), dealership.Id, "T1", [skill]),
            Technician2 = new Technician(Guid.NewGuid(), dealership.Id, "T2", [skill]),
            Bay1 = new ServiceBay(Guid.NewGuid(), dealership.Id, "B1", [capability]),
            Bay2 = new ServiceBay(Guid.NewGuid(), dealership.Id, "B2", [capability])
        };

        db.AddRange(dealership, customer, world.Vehicle, skill, capability, world.OneHourService,
            world.Technician1, world.Technician2, world.Bay1, world.Bay2);
        await db.SaveChangesAsync();
        return world;
    }

    public Appointment Book(Technician technician, ServiceBay bay, DateTimeOffset startAt) =>
        Appointment.Book(Guid.NewGuid(), Customer, Vehicle, Dealership, OneHourService, technician, bay, startAt, DateTimeOffset.UtcNow);
}
