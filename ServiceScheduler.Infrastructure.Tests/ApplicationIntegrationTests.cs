using Microsoft.EntityFrameworkCore;
using ServiceScheduler.Application.DTOs;
using ServiceScheduler.Application.Interfaces;
using ServiceScheduler.Application.Services;
using ServiceScheduler.Domain.Exceptions;
using ServiceScheduler.Infrastructure.Persistence;
using ServiceScheduler.Infrastructure.Repositories;
using ServiceScheduler.Infrastructure.Services;
using static ServiceScheduler.Infrastructure.Tests.TestWorld;

namespace ServiceScheduler.Infrastructure.Tests;

/// <summary>The real Application services on real repositories and a real PostgreSQL.</summary>
public class ApplicationIntegrationTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static BookingService NewBooking(SchedulerDbContext db) => new(
        new CustomerRepository(db), new VehicleRepository(db), new DealershipRepository(db), new ServiceTypeRepository(db),
        new AppointmentRepository(db), new ResourceSelector(new TechnicianRepository(db), new ServiceBayRepository(db)),
        new IdempotencyStore(db), new UnitOfWork(db), new SystemClock());

    private static AvailabilityService NewAvailability(SchedulerDbContext db) => new(
        new DealershipRepository(db), new ServiceTypeRepository(db),
        new ResourceSelector(new TechnicianRepository(db), new ServiceBayRepository(db)));

    private static string NewKey() => Guid.NewGuid().ToString();

    private static BookAppointmentRequest Request(TestWorld w, DateTimeOffset startAt) =>
        new(w.Customer.Id, w.Vehicle.Id, w.Dealership.Id, w.OneHourService.Id, startAt);

    private async Task<TestWorld> SeedAsync()
    {
        await using var db = fixture.CreateContext();
        return await TestWorld.SeedAsync(db);
    }

    [Fact]
    public async Task Booking_persists_the_appointment_and_idempotency_record_together()
    {
        var w = await SeedAsync();
        var key = NewKey();

        await using var db = fixture.CreateContext();
        var dto = await NewBooking(db).BookAsync(key, Request(w, At(10)));

        await using var read = fixture.CreateContext();
        var stored = await new AppointmentRepository(read).GetByIdAsync(dto.Id);
        var record = await new IdempotencyStore(read).FindAsync(key);
        Assert.NotNull(stored);
        Assert.Equal(At(10), stored.StartAt);
        Assert.Equal(At(11), stored.EndAt);
        Assert.NotNull(record);
        Assert.Equal(dto, System.Text.Json.JsonSerializer.Deserialize<AppointmentDto>(record.ResponseJson));
    }

    [Fact]
    public async Task Duplicate_key_commit_fails_as_DuplicateIdempotencyKey_and_rolls_back_the_appointment_too()
    {
        var w = await SeedAsync();
        var key = NewKey();
        await using var first = fixture.CreateContext();
        await NewBooking(first).BookAsync(key, Request(w, At(10)));

        // A second transaction stages a *different* appointment together with a record reusing the key.
        await using var second = fixture.CreateContext();
        var other = w.Book(w.Technician2, w.Bay2, At(14));
        await new AppointmentRepository(second).AddAsync(other);
        await new IdempotencyStore(second).AddAsync(new IdempotencyRecord(key, "hash", "{}", DateTimeOffset.UtcNow));

        await Assert.ThrowsAsync<DuplicateIdempotencyKeyException>(() => new UnitOfWork(second).SaveChangesAsync());

        await using var read = fixture.CreateContext();
        Assert.False(await read.Appointments.AnyAsync(a => a.Id == other.Id)); // atomic: nothing partial
    }

    [Fact]
    public async Task Retry_with_the_same_key_returns_the_same_appointment_and_creates_no_duplicate()
    {
        var w = await SeedAsync();
        var key = NewKey();

        await using var db1 = fixture.CreateContext();
        var first = await NewBooking(db1).BookAsync(key, Request(w, At(10)));
        await using var db2 = fixture.CreateContext();
        var second = await NewBooking(db2).BookAsync(key, Request(w, At(10)));

        Assert.Equal(first, second with { IsReplay = false });
        await using var read = fixture.CreateContext();
        Assert.Equal(1, await read.Appointments.CountAsync(a => a.DealershipId == w.Dealership.Id));
    }

    [Fact]
    public async Task Same_key_with_a_different_payload_is_rejected()
    {
        var w = await SeedAsync();
        var key = NewKey();
        await using var db1 = fixture.CreateContext();
        await NewBooking(db1).BookAsync(key, Request(w, At(10)));

        await using var db2 = fixture.CreateContext();
        await Assert.ThrowsAsync<IdempotencyConflictException>(() => NewBooking(db2).BookAsync(key, Request(w, At(12))));
    }

    [Fact]
    public async Task Concurrent_requests_with_the_same_key_produce_one_appointment_and_one_shared_result()
    {
        var w = await SeedAsync();
        var key = NewKey();
        var gate = new TaskCompletionSource();

        var tasks = Enumerable.Range(0, 10).Select(async _ =>
        {
            await using var db = fixture.CreateContext();
            await gate.Task;
            return await NewBooking(db).BookAsync(key, Request(w, At(10)));
        }).ToList();
        gate.SetResult();
        var results = await Task.WhenAll(tasks);

        Assert.Single(results.Select(r => r.Id).Distinct());
        await using var read = fixture.CreateContext();
        Assert.Equal(1, await read.Appointments.CountAsync(a => a.DealershipId == w.Dealership.Id));
    }

    [Fact]
    public async Task Concurrent_bookings_for_one_slot_fill_every_disjoint_pair_and_the_rest_get_a_conflict()
    {
        var w = await SeedAsync(); // 2 technicians x 2 bays => exactly 2 simultaneous appointments possible
        var gate = new TaskCompletionSource();

        var tasks = Enumerable.Range(0, 8).Select(async _ =>
        {
            await using var db = fixture.CreateContext();
            await gate.Task;
            try
            {
                return await NewBooking(db).BookAsync(NewKey(), Request(w, At(10)));
            }
            catch (ResourceUnavailableException)
            {
                return null;
            }
        }).ToList();
        gate.SetResult();
        var winners = (await Task.WhenAll(tasks)).OfType<AppointmentDto>().ToList();

        Assert.Equal(2, winners.Count);
        Assert.Equal(2, winners.Select(a => a.TechnicianId).Distinct().Count());
        Assert.Equal(2, winners.Select(a => a.ServiceBayId).Distinct().Count());
        await using var read = fixture.CreateContext();
        Assert.Equal(2, await read.Appointments.CountAsync(a => a.DealershipId == w.Dealership.Id));
    }

    [Fact]
    public async Task Cancellation_persists_status_and_record_and_frees_the_slot_for_rebooking()
    {
        var w = await SeedAsync();
        await using var db1 = fixture.CreateContext();
        var booking = NewBooking(db1);
        // Occupy the second pair so the freed pair is the only possible one.
        await using var dbOther = fixture.CreateContext();
        var other = await NewBooking(dbOther).BookAsync(NewKey(), Request(w, At(10)));
        var booked = await booking.BookAsync(NewKey(), Request(w, At(10)));
        await using var dbFull = fixture.CreateContext();
        Assert.False((await NewAvailability(dbFull).CheckAsync(new AvailabilityRequest(w.Dealership.Id, w.OneHourService.Id, At(10)))).IsAvailable);

        var cancelKey = NewKey();
        await using var db2 = fixture.CreateContext();
        var cancelled = await NewBooking(db2).CancelAsync(cancelKey, booked.Id);

        Assert.Equal("Cancelled", cancelled.Status);
        await using var read = fixture.CreateContext();
        Assert.Equal(Domain.Enums.AppointmentStatus.Cancelled, (await new AppointmentRepository(read).GetByIdAsync(booked.Id))!.Status);
        Assert.NotNull(await new IdempotencyStore(read).FindAsync(cancelKey));

        await using var db3 = fixture.CreateContext();
        Assert.True((await NewAvailability(db3).CheckAsync(new AvailabilityRequest(w.Dealership.Id, w.OneHourService.Id, At(10)))).IsAvailable);
        var rebooked = await NewBooking(db3).BookAsync(NewKey(), Request(w, At(10)));
        Assert.Equal((booked.TechnicianId, booked.ServiceBayId), (rebooked.TechnicianId, rebooked.ServiceBayId));
        Assert.NotEqual(other.TechnicianId, rebooked.TechnicianId);
    }

    [Fact]
    public async Task Retrying_a_cancellation_with_the_same_key_is_a_replay_and_a_new_key_is_an_invalid_transition()
    {
        var w = await SeedAsync();
        await using var db1 = fixture.CreateContext();
        var booked = await NewBooking(db1).BookAsync(NewKey(), Request(w, At(10)));
        var key = NewKey();
        await using var db2 = fixture.CreateContext();
        var first = await NewBooking(db2).CancelAsync(key, booked.Id);

        await using var db3 = fixture.CreateContext();
        Assert.Equal(first, (await NewBooking(db3).CancelAsync(key, booked.Id)) with { IsReplay = false });
        await using var db4 = fixture.CreateContext();
        await Assert.ThrowsAsync<InvalidStateTransitionException>(() => NewBooking(db4).CancelAsync(NewKey(), booked.Id));
    }

    [Fact]
    public async Task Availability_check_creates_no_appointment_or_record()
    {
        var w = await SeedAsync();
        await using var db = fixture.CreateContext();

        var result = await NewAvailability(db).CheckAsync(new AvailabilityRequest(w.Dealership.Id, w.OneHourService.Id, At(10)));

        Assert.True(result.IsAvailable);
        await using var read = fixture.CreateContext();
        Assert.Equal(0, await read.Appointments.CountAsync(a => a.DealershipId == w.Dealership.Id));
    }
}
