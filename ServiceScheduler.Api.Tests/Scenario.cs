using ServiceScheduler.Domain.Entities;

namespace ServiceScheduler.Api.Tests;

/// <summary>A seeded dealership (08:00-18:00 UTC daily) with a 1h service, <c>technicians</c> technicians and <c>bays</c> bays.</summary>
public sealed record Scenario(Guid CustomerId, Guid VehicleId, Guid DealershipId, Guid ServiceTypeId)
{
    public static string At(int hour, int minute = 0) => $"2030-01-07T{hour:00}:{minute:00}:00Z"; // a Monday

    public object BookingBody(string startAt, Guid? customerId = null, Guid? vehicleId = null) => new
    {
        customerId = customerId ?? CustomerId,
        vehicleId = vehicleId ?? VehicleId,
        dealershipId = DealershipId,
        serviceTypeId = ServiceTypeId,
        startAt
    };

    public object AvailabilityBody(string startAt) => new { dealershipId = DealershipId, serviceTypeId = ServiceTypeId, startAt };

    public static async Task<Scenario> SeedAsync(DatabaseApiFactory factory, int technicians = 2, int bays = 2)
    {
        var dealership = new Dealership(Guid.NewGuid(), "Dealer", "UTC");
        foreach (var day in Enum.GetValues<DayOfWeek>())
        {
            dealership.SetWorkingHours(day, new TimeOnly(8, 0), new TimeOnly(18, 0));
        }

        var skill = new Skill(Guid.NewGuid(), "Brakes");
        var capability = new Capability(Guid.NewGuid(), "Lift");
        var customer = new Customer(Guid.NewGuid(), "Jane", "jane@example.com");
        var vehicle = new Vehicle(Guid.NewGuid(), customer.Id, "VIN-1", "Make", "Model");
        var service = new ServiceType(Guid.NewGuid(), "Brake service", TimeSpan.FromHours(1), [skill], [capability]);

        await factory.WithDbAsync(async db =>
        {
            db.AddRange(dealership, skill, capability, customer, vehicle, service);
            db.AddRange(Enumerable.Range(1, technicians).Select(i => new Technician(Guid.NewGuid(), dealership.Id, $"T{i}", [skill])));
            db.AddRange(Enumerable.Range(1, bays).Select(i => new ServiceBay(Guid.NewGuid(), dealership.Id, $"B{i}", [capability])));
            await db.SaveChangesAsync();
            return 0;
        });

        return new Scenario(customer.Id, vehicle.Id, dealership.Id, service.Id);
    }
}
