using ServiceScheduler.Domain.Exceptions;
using static ServiceScheduler.Application.Tests.World;

namespace ServiceScheduler.Application.Tests;

public class IdempotencyTests
{
    private readonly World _w = new();

    [Fact]
    public async Task Same_key_and_same_request_returns_the_original_without_new_work()
    {
        var key = Key();
        var first = await _w.NewBooking().BookAsync(key, _w.Request(At(10)));
        var queriesBefore = _w.Store.ResourceQueries;

        var second = await _w.NewBooking().BookAsync(key, _w.Request(At(10)));

        Assert.Equal(first, second with { IsReplay = false });
        Assert.Equal(1, _w.Store.SaveCalls);
        Assert.Equal(queriesBefore, _w.Store.ResourceQueries);
        Assert.Single(_w.Store.Appointments);
    }

    [Fact]
    public async Task The_same_instant_sent_with_a_different_offset_is_the_same_request()
    {
        var key = Key();
        var first = await _w.NewBooking().BookAsync(key, _w.Request(At(10)));

        var second = await _w.NewBooking().BookAsync(key,
            _w.Request(new DateTimeOffset(2030, 1, 7, 15, 30, 0, TimeSpan.FromHours(5.5))));

        Assert.Equal(first, second with { IsReplay = false });
        Assert.Single(_w.Store.Appointments);
    }

    [Fact]
    public async Task Same_key_with_a_different_request_is_rejected_and_changes_nothing()
    {
        var key = Key();
        await _w.NewBooking().BookAsync(key, _w.Request(At(10)));

        await Assert.ThrowsAsync<IdempotencyConflictException>(() => _w.NewBooking().BookAsync(key, _w.Request(At(12))));

        Assert.Equal(1, _w.Store.SaveCalls);
        Assert.Single(_w.Store.Appointments);
    }

    [Fact]
    public async Task A_key_used_for_booking_cannot_be_reused_for_cancellation()
    {
        var key = Key();
        var booked = await _w.NewBooking().BookAsync(key, _w.Request(At(10)));

        await Assert.ThrowsAsync<IdempotencyConflictException>(() => _w.NewBooking().CancelAsync(key, booked.Id));
    }

    [Fact]
    public async Task Retry_returns_the_original_response_even_after_the_appointment_was_cancelled()
    {
        var key = Key();
        var booked = await _w.NewBooking().BookAsync(key, _w.Request(At(10)));
        await _w.NewBooking().CancelAsync(Key(), booked.Id);

        var replay = await _w.NewBooking().BookAsync(key, _w.Request(At(10)));

        Assert.Equal(booked, replay with { IsReplay = false });
        Assert.Equal("Confirmed", replay.Status);
    }

    [Fact]
    public async Task Concurrent_duplicate_committed_first_is_returned_instead_of_a_second_appointment()
    {
        var key = Key();
        var request = _w.Request(At(10));
        Application.DTOs.AppointmentDto? winner = null;
        // The racing twin commits between our staging and our commit; our commit then hits the unique key.
        _w.Store.FailNextSave(new DuplicateIdempotencyKeyException(),
            sideEffect: () => winner = _w.NewBooking().BookAsync(key, request).GetAwaiter().GetResult());

        var result = await _w.NewBooking().BookAsync(key, request);

        Assert.NotNull(winner);
        Assert.Equal(winner, result with { IsReplay = false });
        Assert.Single(_w.Store.Appointments);
        Assert.Single(_w.Store.Records);
    }

    [Fact]
    public async Task Twin_that_loses_every_pair_returns_the_twins_result_instead_of_a_conflict()
    {
        var key = Key();
        var request = _w.Request(At(10));
        Application.DTOs.AppointmentDto? winner = null;
        for (var i = 0; i < 3; i++)
        {
            _w.Store.FailNextSave(new ResourceUnavailableException("race"));
        }

        // On our last pair the twin commits its booking; every pair we tried then looks taken.
        _w.Store.FailNextSave(new ResourceUnavailableException("race"),
            sideEffect: () => winner = _w.NewBooking().BookAsync(key, request).GetAwaiter().GetResult());

        var result = await _w.NewBooking().BookAsync(key, request);

        Assert.NotNull(winner);
        Assert.Equal(winner, result with { IsReplay = false });
        Assert.Single(_w.Store.Appointments);
    }

    [Fact]
    public async Task Concurrent_duplicate_with_a_different_request_is_an_idempotency_conflict()
    {
        var key = Key();
        _w.Store.FailNextSave(new DuplicateIdempotencyKeyException(),
            sideEffect: () => _w.NewBooking().BookAsync(key, _w.Request(At(14))).GetAwaiter().GetResult());

        await Assert.ThrowsAsync<IdempotencyConflictException>(() => _w.NewBooking().BookAsync(key, _w.Request(At(10))));

        Assert.Single(_w.Store.Appointments);
    }
}
