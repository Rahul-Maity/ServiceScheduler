using ServiceScheduler.Application.DTOs;
using ServiceScheduler.Application.Interfaces;
using ServiceScheduler.Application.Services;
using ServiceScheduler.Domain.Entities;

namespace ServiceScheduler.Application.Tests;

public sealed class FakeClock(DateTimeOffset now) : IClock
{
    public DateTimeOffset UtcNow { get; set; } = now;
}

/// <summary>A seeded dealership: 08:00-18:00 UTC daily, a 1h service needing one skill + one capability, 2 technicians, 2 bays.</summary>
public sealed class World
{
    public static readonly DateTimeOffset Day = new(2030, 1, 7, 0, 0, 0, TimeSpan.Zero); // a Monday

    public static DateTimeOffset At(int hour, int minute = 0) => Day.AddHours(hour).AddMinutes(minute);

    public static string Key() => Guid.NewGuid().ToString();

    public FakeStore Store { get; } = new();
    public FakeClock Clock { get; } = new(new DateTimeOffset(2029, 12, 1, 9, 0, 0, TimeSpan.Zero));

    public Dealership Dealership { get; }
    public Customer Customer { get; }
    public Vehicle Vehicle { get; }
    public Skill Skill { get; }
    public Capability Capability { get; }
    public ServiceType Service { get; }
    public Technician T1 { get; }
    public Technician T2 { get; }
    public ServiceBay B1 { get; }
    public ServiceBay B2 { get; }

    public World()
    {
        Dealership = new Dealership(Guid.NewGuid(), "Dealer", "UTC");
        foreach (var day in Enum.GetValues<DayOfWeek>())
        {
            Dealership.SetWorkingHours(day, new TimeOnly(8, 0), new TimeOnly(18, 0));
        }

        Skill = new Skill(Guid.NewGuid(), "Brakes");
        Capability = new Capability(Guid.NewGuid(), "Lift");
        Customer = new Customer(Guid.NewGuid(), "Jane", "jane@example.com");
        Vehicle = new Vehicle(Guid.NewGuid(), Customer.Id, "VIN1", "Make", "Model");
        Service = new ServiceType(Guid.NewGuid(), "Brake service", TimeSpan.FromHours(1), [Skill], [Capability]);
        T1 = new Technician(Guid.NewGuid(), Dealership.Id, "T1", [Skill]);
        T2 = new Technician(Guid.NewGuid(), Dealership.Id, "T2", [Skill]);
        B1 = new ServiceBay(Guid.NewGuid(), Dealership.Id, "B1", [Capability]);
        B2 = new ServiceBay(Guid.NewGuid(), Dealership.Id, "B2", [Capability]);

        Store.Dealerships[Dealership.Id] = Dealership;
        Store.Customers[Customer.Id] = Customer;
        Store.Vehicles[Vehicle.Id] = Vehicle;
        Store.ServiceTypes[Service.Id] = Service;
        Store.Technicians.AddRange([T1, T2]);
        Store.Bays.AddRange([B1, B2]);
    }

    public BookingService NewBooking() =>
        new(Store, Store, Store, Store, Store, new ResourceSelector(Store, Store), Store, Store, Clock);

    public AvailabilityService NewAvailability() =>
        new(Store, Store, new ResourceSelector(Store, Store));

    public BookAppointmentRequest Request(DateTimeOffset startAt) =>
        new(Customer.Id, Vehicle.Id, Dealership.Id, Service.Id, startAt);

    public AvailabilityRequest AvailabilityFor(DateTimeOffset startAt) =>
        new(Dealership.Id, Service.Id, startAt);

    /// <summary>Directly places an already-confirmed appointment (for the 1h service) in the committed state.</summary>
    public Appointment PlaceConfirmed(Technician technician, ServiceBay bay, DateTimeOffset startAt)
    {
        var appointment = Appointment.Book(Guid.NewGuid(), Customer, Vehicle, Dealership, Service, technician, bay, startAt, Clock.UtcNow);
        Store.Appointments.Add(appointment);
        return appointment;
    }
}
