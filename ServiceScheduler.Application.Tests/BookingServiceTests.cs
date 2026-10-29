using System.Text.Json;
using ServiceScheduler.Application.DTOs;
using ServiceScheduler.Domain.Entities;
using ServiceScheduler.Domain.Exceptions;
using static ServiceScheduler.Application.Tests.World;

namespace ServiceScheduler.Application.Tests;

public class BookingServiceTests
{
    private readonly World _w = new();

    [Fact]
    public async Task Books_a_confirmed_appointment_with_computed_utc_interval_and_first_suitable_pair()
    {
        var key = Key();

        var dto = await _w.NewBooking().BookAsync(key, _w.Request(At(10)));

        Assert.Equal("Confirmed", dto.Status);
        Assert.Equal(_w.Customer.Id, dto.CustomerId);
        Assert.Equal(_w.Vehicle.Id, dto.VehicleId);
        Assert.Equal(_w.Dealership.Id, dto.DealershipId);
        Assert.Equal(_w.Service.Id, dto.ServiceTypeId);
        Assert.Equal(_w.T1.Id, dto.TechnicianId);
        Assert.Equal(_w.B1.Id, dto.ServiceBayId);
        Assert.Equal(At(10), dto.StartAt);
        Assert.Equal(At(11), dto.EndAt);
        Assert.Equal(_w.Clock.UtcNow, dto.CreatedAt);
        Assert.Null(dto.CancelledAt);
        Assert.Equal(dto.Id, Assert.Single(_w.Store.Appointments).Id);
    }

    [Fact]
    public async Task Appointment_and_idempotency_record_are_staged_and_committed_in_one_save()
    {
        var key = Key();

        var dto = await _w.NewBooking().BookAsync(key, _w.Request(At(10)));

        Assert.Equal(1, _w.Store.SaveCalls);
        Assert.Equal((1, 1), _w.Store.LastSaveStaged);
        var record = _w.Store.Records[key];
        Assert.Equal(dto, JsonSerializer.Deserialize<AppointmentDto>(record.ResponseJson));
        Assert.False(string.IsNullOrWhiteSpace(record.RequestHash));
        Assert.Equal(_w.Clock.UtcNow, record.CreatedAt);
    }

    [Fact]
    public async Task Offset_start_time_is_stored_as_utc()
    {
        var dto = await _w.NewBooking().BookAsync(Key(),
            _w.Request(new DateTimeOffset(2030, 1, 7, 15, 30, 0, TimeSpan.FromHours(5.5))));

        Assert.Equal(At(10), dto.StartAt);
        Assert.Equal(TimeSpan.Zero, dto.StartAt.Offset);
        Assert.Equal(At(11), dto.EndAt);
    }

    [Theory]
    [InlineData("customer")]
    [InlineData("vehicle")]
    [InlineData("dealership")]
    [InlineData("service")]
    public async Task Unknown_reference_is_not_found_and_saves_nothing(string missing)
    {
        var request = missing switch
        {
            "customer" => _w.Request(At(10)) with { CustomerId = Guid.NewGuid() },
            "vehicle" => _w.Request(At(10)) with { VehicleId = Guid.NewGuid() },
            "dealership" => _w.Request(At(10)) with { DealershipId = Guid.NewGuid() },
            _ => _w.Request(At(10)) with { ServiceTypeId = Guid.NewGuid() }
        };

        await Assert.ThrowsAsync<EntityNotFoundException>(() => _w.NewBooking().BookAsync(Key(), request));

        Assert.Equal(0, _w.Store.SaveCalls);
    }

    [Fact]
    public async Task Vehicle_of_another_customer_is_a_validation_error()
    {
        var other = new Customer(Guid.NewGuid(), "Other", "o@example.com");
        _w.Store.Customers[other.Id] = other;

        await Assert.ThrowsAsync<DomainValidationException>(() =>
            _w.NewBooking().BookAsync(Key(), _w.Request(At(10)) with { CustomerId = other.Id }));
        Assert.Equal(0, _w.Store.SaveCalls);
    }

    [Fact]
    public async Task Outside_working_hours_is_a_validation_error_even_when_nothing_is_free()
    {
        _w.Store.Technicians.Clear();

        await Assert.ThrowsAsync<DomainValidationException>(() =>
            _w.NewBooking().BookAsync(Key(), _w.Request(At(20))));
        Assert.Equal(0, _w.Store.SaveCalls);
    }

    [Fact]
    public async Task Invalid_input_is_a_validation_error()
    {
        var service = _w.NewBooking();

        await Assert.ThrowsAsync<DomainValidationException>(() => service.BookAsync("", _w.Request(At(10))));
        await Assert.ThrowsAsync<DomainValidationException>(() =>
            service.BookAsync(Key(), _w.Request(At(10)) with { CustomerId = Guid.Empty }));
    }

    [Fact]
    public async Task No_qualified_technician_is_a_conflict_with_nothing_persisted()
    {
        _w.Store.Technicians.Clear();
        _w.Store.Technicians.Add(new Technician(Guid.NewGuid(), _w.Dealership.Id, "Unskilled", []));
        var key = Key();

        await Assert.ThrowsAsync<ResourceUnavailableException>(() => _w.NewBooking().BookAsync(key, _w.Request(At(10))));

        Assert.Equal(0, _w.Store.SaveCalls);
        Assert.Empty(_w.Store.Appointments);
        Assert.Empty(_w.Store.Records);
    }

    [Fact]
    public async Task No_compatible_bay_is_a_conflict()
    {
        _w.Store.Bays.Clear();
        _w.Store.Bays.Add(new ServiceBay(Guid.NewGuid(), _w.Dealership.Id, "No lift", []));

        await Assert.ThrowsAsync<ResourceUnavailableException>(() => _w.NewBooking().BookAsync(Key(), _w.Request(At(10))));
    }

    [Fact]
    public async Task Skips_a_busy_technician_and_a_busy_bay()
    {
        _w.PlaceConfirmed(_w.T1, _w.B1, At(10));

        var dto = await _w.NewBooking().BookAsync(Key(), _w.Request(At(10, 30)));

        Assert.Equal(_w.T2.Id, dto.TechnicianId);
        Assert.Equal(_w.B2.Id, dto.ServiceBayId);
    }

    [Fact]
    public async Task Back_to_back_booking_may_reuse_the_same_resources()
    {
        _w.PlaceConfirmed(_w.T1, _w.B1, At(10));

        var dto = await _w.NewBooking().BookAsync(Key(), _w.Request(At(11)));

        Assert.Equal(_w.T1.Id, dto.TechnicianId);
        Assert.Equal(_w.B1.Id, dto.ServiceBayId);
    }

    [Fact]
    public async Task One_minute_overlap_on_all_resources_is_a_conflict()
    {
        _w.PlaceConfirmed(_w.T1, _w.B1, At(10));
        _w.PlaceConfirmed(_w.T2, _w.B2, At(10));

        await Assert.ThrowsAsync<ResourceUnavailableException>(() =>
            _w.NewBooking().BookAsync(Key(), _w.Request(At(10, 59))));
    }

    [Fact]
    public async Task Lost_race_on_the_first_pair_falls_back_to_the_next_pair()
    {
        _w.Store.FailNextSave(new ResourceUnavailableException("race"));

        var dto = await _w.NewBooking().BookAsync(Key(), _w.Request(At(10)));

        Assert.Equal(2, _w.Store.SaveCalls);
        Assert.Equal((_w.T1.Id, _w.B2.Id), (dto.TechnicianId, dto.ServiceBayId));
        Assert.Single(_w.Store.Appointments);
        Assert.Single(_w.Store.Records);
    }

    [Fact]
    public async Task When_every_pair_loses_the_race_it_is_a_conflict_and_nothing_is_persisted()
    {
        for (var i = 0; i < 4; i++)
        {
            _w.Store.FailNextSave(new ResourceUnavailableException("race"));
        }

        var key = Key();
        await Assert.ThrowsAsync<ResourceUnavailableException>(() => _w.NewBooking().BookAsync(key, _w.Request(At(10))));

        Assert.Equal(4, _w.Store.SaveCalls);
        Assert.Equal(4, _w.Store.SaveAttempts.Distinct().Count()); // each pair tried exactly once
        Assert.Empty(_w.Store.Appointments);
        Assert.Empty(_w.Store.Records);
    }

    [Fact]
    public async Task A_failed_booking_does_not_burn_the_idempotency_key()
    {
        for (var i = 0; i < 4; i++)
        {
            _w.Store.FailNextSave(new ResourceUnavailableException("race"));
        }

        var key = Key();
        await Assert.ThrowsAsync<ResourceUnavailableException>(() => _w.NewBooking().BookAsync(key, _w.Request(At(10))));

        var retry = await _w.NewBooking().BookAsync(key, _w.Request(At(10)));

        Assert.Equal("Confirmed", retry.Status);
        Assert.Single(_w.Store.Records);
    }

    [Fact]
    public async Task Unexpected_failures_propagate_and_are_not_retried()
    {
        _w.Store.FailNextSave(new InvalidOperationException("boom"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _w.NewBooking().BookAsync(Key(), _w.Request(At(10))));

        Assert.Equal(1, _w.Store.SaveCalls);
        Assert.Empty(_w.Store.Appointments);
    }

    [Fact]
    public async Task Get_returns_the_appointment_or_not_found()
    {
        var booked = await _w.NewBooking().BookAsync(Key(), _w.Request(At(10)));

        Assert.Equal(booked, await _w.NewBooking().GetAsync(booked.Id));
        await Assert.ThrowsAsync<EntityNotFoundException>(() => _w.NewBooking().GetAsync(Guid.NewGuid()));
    }
}
