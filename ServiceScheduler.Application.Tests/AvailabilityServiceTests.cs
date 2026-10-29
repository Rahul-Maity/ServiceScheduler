using ServiceScheduler.Application.DTOs;
using ServiceScheduler.Domain.Entities;
using ServiceScheduler.Domain.Exceptions;
using static ServiceScheduler.Application.Tests.World;

namespace ServiceScheduler.Application.Tests;

public class AvailabilityServiceTests
{
    private readonly World _w = new();

    [Fact]
    public async Task Free_qualified_technician_and_compatible_bay_is_available_with_computed_utc_window()
    {
        var result = await _w.NewAvailability().CheckAsync(_w.AvailabilityFor(At(10)));

        Assert.True(result.IsAvailable);
        Assert.Equal(At(10), result.StartAt);
        Assert.Equal(At(11), result.EndAt);
    }

    [Fact]
    public async Task Offset_start_is_normalized_to_utc()
    {
        var result = await _w.NewAvailability().CheckAsync(
            _w.AvailabilityFor(new DateTimeOffset(2030, 1, 7, 15, 30, 0, TimeSpan.FromHours(5.5))));

        Assert.Equal(At(10), result.StartAt);
        Assert.Equal(TimeSpan.Zero, result.StartAt.Offset);
        Assert.Equal(At(11), result.EndAt);
    }

    [Fact]
    public async Task Unavailable_when_no_technician_is_qualified()
    {
        _w.Store.Technicians.Clear();
        _w.Store.Technicians.Add(new Technician(Guid.NewGuid(), _w.Dealership.Id, "Unskilled", []));

        Assert.False((await _w.NewAvailability().CheckAsync(_w.AvailabilityFor(At(10)))).IsAvailable);
    }

    [Fact]
    public async Task Unavailable_when_no_bay_is_compatible()
    {
        _w.Store.Bays.Clear();
        _w.Store.Bays.Add(new ServiceBay(Guid.NewGuid(), _w.Dealership.Id, "No lift", []));

        Assert.False((await _w.NewAvailability().CheckAsync(_w.AvailabilityFor(At(10)))).IsAvailable);
    }

    [Fact]
    public async Task Unavailable_when_there_are_no_resources_at_all()
    {
        _w.Store.Technicians.Clear();
        _w.Store.Bays.Clear();

        Assert.False((await _w.NewAvailability().CheckAsync(_w.AvailabilityFor(At(10)))).IsAvailable);
    }

    [Fact]
    public async Task Resources_of_another_dealership_are_ignored()
    {
        _w.Store.Technicians.Clear();
        _w.Store.Technicians.Add(new Technician(Guid.NewGuid(), Guid.NewGuid(), "Elsewhere", [_w.Skill]));

        Assert.False((await _w.NewAvailability().CheckAsync(_w.AvailabilityFor(At(10)))).IsAvailable);
    }

    [Theory]
    [InlineData(7, 30, false)]  // starts before opening
    [InlineData(8, 0, true)]    // starts exactly at opening
    [InlineData(17, 0, true)]   // ends exactly at closing (half-open)
    [InlineData(17, 1, false)]  // ends one minute after closing
    public async Task Working_hours_bound_the_whole_service(int hour, int minute, bool expected)
    {
        var result = await _w.NewAvailability().CheckAsync(_w.AvailabilityFor(At(hour, minute)));

        Assert.Equal(expected, result.IsAvailable);
    }

    [Fact]
    public async Task Overlapping_confirmed_appointments_on_every_technician_make_it_unavailable()
    {
        _w.PlaceConfirmed(_w.T1, _w.B1, At(10));
        _w.PlaceConfirmed(_w.T2, _w.B2, At(10, 30));

        Assert.False((await _w.NewAvailability().CheckAsync(_w.AvailabilityFor(At(10, 30)))).IsAvailable);
    }

    [Fact]
    public async Task Back_to_back_is_available_but_a_one_minute_overlap_is_not()
    {
        _w.PlaceConfirmed(_w.T1, _w.B1, At(10));
        _w.PlaceConfirmed(_w.T2, _w.B2, At(10));

        Assert.True((await _w.NewAvailability().CheckAsync(_w.AvailabilityFor(At(11)))).IsAvailable);
        Assert.False((await _w.NewAvailability().CheckAsync(_w.AvailabilityFor(At(10, 59)))).IsAvailable);
    }

    [Fact]
    public async Task Cancelled_appointments_do_not_block()
    {
        var a1 = _w.PlaceConfirmed(_w.T1, _w.B1, At(10));
        var a2 = _w.PlaceConfirmed(_w.T2, _w.B2, At(10));
        a1.Cancel(_w.Clock.UtcNow);
        a2.Cancel(_w.Clock.UtcNow);

        Assert.True((await _w.NewAvailability().CheckAsync(_w.AvailabilityFor(At(10)))).IsAvailable);
    }

    [Fact]
    public async Task Checking_reserves_nothing()
    {
        await _w.NewAvailability().CheckAsync(_w.AvailabilityFor(At(10)));

        Assert.Equal(0, _w.Store.SaveCalls);
        Assert.Equal(0, _w.Store.StagedCount);
        Assert.Empty(_w.Store.Appointments);
        Assert.Empty(_w.Store.Records);
    }

    [Fact]
    public async Task Unknown_dealership_or_service_type_is_not_found()
    {
        var service = _w.NewAvailability();

        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            service.CheckAsync(new AvailabilityRequest(Guid.NewGuid(), _w.Service.Id, At(10))));
        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            service.CheckAsync(new AvailabilityRequest(_w.Dealership.Id, Guid.NewGuid(), At(10))));
    }

    [Fact]
    public async Task Invalid_request_is_a_validation_error()
    {
        await Assert.ThrowsAsync<DomainValidationException>(() =>
            _w.NewAvailability().CheckAsync(new AvailabilityRequest(Guid.Empty, _w.Service.Id, At(10))));
    }
}
