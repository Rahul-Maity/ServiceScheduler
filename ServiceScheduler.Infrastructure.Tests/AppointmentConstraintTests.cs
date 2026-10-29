using Microsoft.EntityFrameworkCore;
using ServiceScheduler.Domain.Entities;
using ServiceScheduler.Domain.Exceptions;
using ServiceScheduler.Infrastructure.Persistence;
using ServiceScheduler.Infrastructure.Repositories;
using static ServiceScheduler.Infrastructure.Tests.TestWorld;

namespace ServiceScheduler.Infrastructure.Tests;

/// <summary>The database, not application code, must reject overlapping Confirmed appointments.</summary>
public class AppointmentConstraintTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static async Task SaveAsync(SchedulerDbContext db, Appointment appointment)
    {
        await new AppointmentRepository(db).AddAsync(appointment);
        await new UnitOfWork(db).SaveChangesAsync();
    }

    private async Task<(TestWorld World, SchedulerDbContext Db)> ArrangeAsync()
    {
        var db = fixture.CreateContext();
        return (await SeedAsync(db), db);
    }

    [Fact]
    public async Task Same_technician_overlapping_is_rejected_even_with_different_bay()
    {
        var (w, db) = await ArrangeAsync();
        await SaveAsync(db, w.Book(w.Technician1, w.Bay1, At(10)));

        await using var db2 = fixture.CreateContext();
        await Assert.ThrowsAsync<ResourceUnavailableException>(() =>
            SaveAsync(db2, w.Book(w.Technician1, w.Bay2, At(10, 30))));
    }

    [Fact]
    public async Task Same_bay_overlapping_is_rejected_even_with_different_technician()
    {
        var (w, db) = await ArrangeAsync();
        await SaveAsync(db, w.Book(w.Technician1, w.Bay1, At(10)));

        await using var db2 = fixture.CreateContext();
        await Assert.ThrowsAsync<ResourceUnavailableException>(() =>
            SaveAsync(db2, w.Book(w.Technician2, w.Bay1, At(10, 30))));
    }

    [Fact]
    public async Task Overlap_by_one_minute_is_rejected()
    {
        var (w, db) = await ArrangeAsync();
        await SaveAsync(db, w.Book(w.Technician1, w.Bay1, At(10))); // 10:00-11:00

        await using var db2 = fixture.CreateContext();
        await Assert.ThrowsAsync<ResourceUnavailableException>(() =>
            SaveAsync(db2, w.Book(w.Technician1, w.Bay1, At(10, 59))));
    }

    [Fact]
    public async Task Back_to_back_appointments_are_allowed()
    {
        var (w, db) = await ArrangeAsync();
        await SaveAsync(db, w.Book(w.Technician1, w.Bay1, At(10))); // 10:00-11:00

        await using var db2 = fixture.CreateContext();
        await SaveAsync(db2, w.Book(w.Technician1, w.Bay1, At(11))); // 11:00-12:00
        await SaveAsync(db2, w.Book(w.Technician1, w.Bay1, At(9)));  // 09:00-10:00

        Assert.Equal(3, await db2.Appointments.CountAsync(a => a.TechnicianId == w.Technician1.Id));
    }

    [Fact]
    public async Task Different_technician_and_bay_may_overlap()
    {
        var (w, db) = await ArrangeAsync();
        await SaveAsync(db, w.Book(w.Technician1, w.Bay1, At(10)));

        await using var db2 = fixture.CreateContext();
        await SaveAsync(db2, w.Book(w.Technician2, w.Bay2, At(10)));

        Assert.Equal(2, await db2.Appointments.CountAsync(a => a.DealershipId == w.Dealership.Id));
    }

    [Fact]
    public async Task Cancelled_appointment_does_not_block_and_cancellation_frees_the_slot()
    {
        var (w, db) = await ArrangeAsync();
        var first = w.Book(w.Technician1, w.Bay1, At(10));
        await SaveAsync(db, first);

        first.Cancel(DateTimeOffset.UtcNow);
        await new UnitOfWork(db).SaveChangesAsync();

        await using var db2 = fixture.CreateContext();
        await SaveAsync(db2, w.Book(w.Technician1, w.Bay1, At(10)));

        Assert.Equal(1, await db2.Appointments.CountAsync(a =>
            a.TechnicianId == w.Technician1.Id && a.Status == Domain.Enums.AppointmentStatus.Confirmed));
    }

    [Fact]
    public async Task Failed_save_leaves_no_partial_state_and_the_context_can_retry_with_another_resource()
    {
        var (w, db) = await ArrangeAsync();
        await SaveAsync(db, w.Book(w.Technician1, w.Bay1, At(10)));

        await using var db2 = fixture.CreateContext();
        var losing = w.Book(w.Technician2, w.Bay1, At(10)); // bay clash
        await Assert.ThrowsAsync<ResourceUnavailableException>(() => SaveAsync(db2, losing));
        Assert.False(await db2.Appointments.AnyAsync(a => a.Id == losing.Id));

        // Same context, fallback to the other bay.
        await SaveAsync(db2, w.Book(w.Technician2, w.Bay2, At(10)));
        Assert.Equal(2, await db2.Appointments.CountAsync(a => a.DealershipId == w.Dealership.Id));
    }

    [Fact]
    public async Task Offset_start_time_is_stored_as_the_same_utc_instant()
    {
        var (w, db) = await ArrangeAsync();
        var local = new DateTimeOffset(2030, 1, 7, 15, 30, 0, TimeSpan.FromHours(5.5)); // 10:00 UTC
        var appointment = w.Book(w.Technician1, w.Bay1, local);
        await SaveAsync(db, appointment);

        await using var db2 = fixture.CreateContext();
        var stored = await new AppointmentRepository(db2).GetByIdAsync(appointment.Id);

        Assert.NotNull(stored);
        Assert.Equal(At(10), stored.StartAt);
        Assert.Equal(At(11), stored.EndAt);
        Assert.Equal(TimeSpan.Zero, stored.StartAt.Offset);
    }

    [Fact]
    public async Task Concurrent_bookings_for_the_same_slot_yield_exactly_one_success()
    {
        var (w, _) = await ArrangeAsync();
        const int attempts = 12;
        var start = new TaskCompletionSource();

        var tasks = Enumerable.Range(0, attempts).Select(async _ =>
        {
            await using var db = fixture.CreateContext();
            var appointment = w.Book(w.Technician1, w.Bay1, At(10));
            await start.Task;
            try
            {
                await SaveAsync(db, appointment);
                return true;
            }
            catch (ResourceUnavailableException)
            {
                return false;
            }
        }).ToList();

        start.SetResult();
        var results = await Task.WhenAll(tasks);

        Assert.Equal(1, results.Count(r => r));
        await using var check = fixture.CreateContext();
        Assert.Equal(1, await check.Appointments.CountAsync(a => a.TechnicianId == w.Technician1.Id));
    }
}
