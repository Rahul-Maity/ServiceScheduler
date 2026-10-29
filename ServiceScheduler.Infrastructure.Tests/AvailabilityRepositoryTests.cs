using ServiceScheduler.Domain.Entities;
using ServiceScheduler.Infrastructure.Persistence;
using ServiceScheduler.Infrastructure.Repositories;
using static ServiceScheduler.Infrastructure.Tests.TestWorld;

namespace ServiceScheduler.Infrastructure.Tests;

public class AvailabilityRepositoryTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static async Task SaveAsync(SchedulerDbContext db, Appointment appointment)
    {
        await new AppointmentRepository(db).AddAsync(appointment);
        await new UnitOfWork(db).SaveChangesAsync();
    }

    [Fact]
    public async Task Reference_data_loads_with_skills_capabilities_and_working_hours()
    {
        await using var db = fixture.CreateContext();
        var w = await SeedAsync(db);

        await using var read = fixture.CreateContext();
        var serviceType = await new ServiceTypeRepository(read).GetByIdAsync(w.OneHourService.Id);
        var dealership = await new DealershipRepository(read).GetByIdAsync(w.Dealership.Id);

        Assert.NotNull(serviceType);
        Assert.Equal(TimeSpan.FromHours(1), serviceType.Duration);
        Assert.Single(serviceType.RequiredSkills);
        Assert.Single(serviceType.RequiredCapabilities);
        Assert.NotNull(dealership);
        Assert.Equal(7, dealership.WorkingHours.Count);
        Assert.True(dealership.IsOpenDuring(At(10), At(11)));
        Assert.NotNull(await new CustomerRepository(read).GetByIdAsync(w.Customer.Id));
        Assert.NotNull(await new VehicleRepository(read).GetByIdAsync(w.Vehicle.Id));
    }

    [Fact]
    public async Task Finds_only_qualified_technicians_and_compatible_bays_of_the_dealership()
    {
        await using var db = fixture.CreateContext();
        var w = await SeedAsync(db);

        var unskilled = new Technician(Guid.NewGuid(), w.Dealership.Id, "Unskilled", []);
        var noLift = new ServiceBay(Guid.NewGuid(), w.Dealership.Id, "No lift", []);
        var other = await SeedAsync(db); // another dealership with its own resources
        db.AddRange(unskilled, noLift);
        await db.SaveChangesAsync();

        await using var read = fixture.CreateContext();
        var technicians = await new TechnicianRepository(read)
            .FindAvailableAsync(w.Dealership.Id, w.OneHourService, At(10), At(11));
        var bays = await new ServiceBayRepository(read)
            .FindAvailableAsync(w.Dealership.Id, w.OneHourService, At(10), At(11));

        Assert.Equal(new[] { w.Technician1.Id, w.Technician2.Id }.Order(), technicians.Select(t => t.Id).Order());
        Assert.Equal(new[] { w.Bay1.Id, w.Bay2.Id }.Order(), bays.Select(b => b.Id).Order());
        Assert.DoesNotContain(technicians, t => t.Id == other.Technician1.Id);
    }

    [Fact]
    public async Task Excludes_resources_with_overlapping_confirmed_appointments_but_not_touching_or_cancelled_ones()
    {
        await using var db = fixture.CreateContext();
        var w = await SeedAsync(db);
        await SaveAsync(db, w.Book(w.Technician1, w.Bay1, At(10)));                 // 10-11 busy
        var cancelled = w.Book(w.Technician2, w.Bay2, At(10));
        await SaveAsync(db, cancelled);
        cancelled.Cancel(DateTimeOffset.UtcNow);
        await new UnitOfWork(db).SaveChangesAsync();

        await using var read = fixture.CreateContext();
        var techs = new TechnicianRepository(read);
        var bays = new ServiceBayRepository(read);

        var during = await techs.FindAvailableAsync(w.Dealership.Id, w.OneHourService, At(10, 30), At(11, 30));
        Assert.Equal(w.Technician2.Id, Assert.Single(during).Id); // cancelled one does not reserve
        var bayDuring = await bays.FindAvailableAsync(w.Dealership.Id, w.OneHourService, At(10, 30), At(11, 30));
        Assert.Equal(w.Bay2.Id, Assert.Single(bayDuring).Id);

        var touching = await techs.FindAvailableAsync(w.Dealership.Id, w.OneHourService, At(11), At(12));
        Assert.Equal(2, touching.Count);
    }

    [Fact]
    public async Task Availability_check_reserves_nothing()
    {
        await using var db = fixture.CreateContext();
        var w = await SeedAsync(db);

        await using var read = fixture.CreateContext();
        await new TechnicianRepository(read).FindAvailableAsync(w.Dealership.Id, w.OneHourService, At(10), At(11));

        Assert.Empty(read.Appointments.Where(a => a.DealershipId == w.Dealership.Id));
    }
}
