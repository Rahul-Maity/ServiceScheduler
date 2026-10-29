using ServiceScheduler.Domain.Exceptions;
using static ServiceScheduler.Application.Tests.World;

namespace ServiceScheduler.Application.Tests;

public class CancellationTests
{
    private readonly World _w = new();

    [Fact]
    public async Task Cancels_the_appointment_and_saves_the_idempotency_record_in_one_commit()
    {
        var booked = await _w.NewBooking().BookAsync(Key(), _w.Request(At(10)));
        _w.Clock.UtcNow = _w.Clock.UtcNow.AddHours(1);
        var key = Key();

        var cancelled = await _w.NewBooking().CancelAsync(key, booked.Id);

        Assert.Equal("Cancelled", cancelled.Status);
        Assert.Equal(_w.Clock.UtcNow, cancelled.CancelledAt);
        Assert.Equal(booked.Id, cancelled.Id);
        Assert.Equal(2, _w.Store.SaveCalls);                 // booking + cancellation
        Assert.Equal((0, 1), _w.Store.LastSaveStaged);        // cancellation commit carries its idempotency record
        Assert.Equal(cancelled, await _w.NewBooking().GetAsync(booked.Id));
        Assert.Contains(key, _w.Store.Records.Keys);
    }

    [Fact]
    public async Task Unknown_appointment_is_not_found()
    {
        await Assert.ThrowsAsync<EntityNotFoundException>(() => _w.NewBooking().CancelAsync(Key(), Guid.NewGuid()));
    }

    [Fact]
    public async Task Cancelling_twice_with_different_keys_is_an_invalid_transition()
    {
        var booked = await _w.NewBooking().BookAsync(Key(), _w.Request(At(10)));
        await _w.NewBooking().CancelAsync(Key(), booked.Id);

        await Assert.ThrowsAsync<InvalidStateTransitionException>(() => _w.NewBooking().CancelAsync(Key(), booked.Id));
    }

    [Fact]
    public async Task Retrying_a_cancellation_with_the_same_key_returns_the_original_result()
    {
        var booked = await _w.NewBooking().BookAsync(Key(), _w.Request(At(10)));
        var key = Key();
        var first = await _w.NewBooking().CancelAsync(key, booked.Id);
        var savesBefore = _w.Store.SaveCalls;

        var second = await _w.NewBooking().CancelAsync(key, booked.Id);

        Assert.Equal(first, second with { IsReplay = false });
        Assert.Equal(savesBefore, _w.Store.SaveCalls);
    }

    [Fact]
    public async Task Reusing_a_cancellation_key_for_another_appointment_is_rejected()
    {
        var a = await _w.NewBooking().BookAsync(Key(), _w.Request(At(10)));
        var b = await _w.NewBooking().BookAsync(Key(), _w.Request(At(12)));
        var key = Key();
        await _w.NewBooking().CancelAsync(key, a.Id);

        await Assert.ThrowsAsync<IdempotencyConflictException>(() => _w.NewBooking().CancelAsync(key, b.Id));
    }

    [Fact]
    public async Task Cancelled_slot_becomes_bookable_again_with_the_same_resources()
    {
        var booked = await _w.NewBooking().BookAsync(Key(), _w.Request(At(10)));
        _w.PlaceConfirmed(_w.T2, _w.B2, At(10)); // occupy the alternative pair so reuse is observable
        await _w.NewBooking().CancelAsync(Key(), booked.Id);

        Assert.True((await _w.NewAvailability().CheckAsync(_w.AvailabilityFor(At(10)))).IsAvailable);
        var rebooked = await _w.NewBooking().BookAsync(Key(), _w.Request(At(10)));

        Assert.Equal((booked.TechnicianId, booked.ServiceBayId), (rebooked.TechnicianId, rebooked.ServiceBayId));
    }

    [Fact]
    public async Task Invalid_key_is_a_validation_error()
    {
        await Assert.ThrowsAsync<DomainValidationException>(() => _w.NewBooking().CancelAsync(" ", Guid.NewGuid()));
    }
}
