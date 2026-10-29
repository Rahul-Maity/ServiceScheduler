using System.Net;
using ServiceScheduler.Domain.Entities;
using ServiceScheduler.Domain.Enums;
using static ServiceScheduler.Api.Tests.Scenario;

namespace ServiceScheduler.Api.Tests;

/// <summary>Controllers + Application + Infrastructure on a real PostgreSQL.</summary>
[Collection("Database")]
public class EndToEndTests(DatabaseApiFactory factory) : DatabaseTestBase(factory)
{
    // ---------- availability ----------

    [Fact]
    public async Task Availability_follows_the_schedule_and_never_reserves_anything()
    {
        var s = await SeedAsync(technicians: 1, bays: 1);
        Assert.True(await IsAvailableAsync(s, At(10)));
        Assert.Equal(0, await AppointmentCountAsync());

        Assert.Equal(HttpStatusCode.Created, (await PostAsync("/api/appointments", s.BookingBody(At(10)), NewKey())).StatusCode);

        Assert.False(await IsAvailableAsync(s, At(10)));       // taken
        Assert.False(await IsAvailableAsync(s, At(10, 59)));   // one-minute overlap
        Assert.True(await IsAvailableAsync(s, At(11)));        // back-to-back
        Assert.True(await IsAvailableAsync(s, At(9)));         // ends exactly when the booked one starts
        Assert.False(await IsAvailableAsync(s, At(7)));        // before opening
        Assert.True(await IsAvailableAsync(s, At(17)));        // ends exactly at closing
        Assert.False(await IsAvailableAsync(s, At(17, 1)));    // would end after closing
        Assert.Equal(1, await AppointmentCountAsync());        // the checks reserved nothing
    }

    [Fact]
    public async Task Availability_response_is_normalized_to_utc()
    {
        var s = await SeedAsync();

        var response = await PostAsync("/api/availability", s.AvailabilityBody("2030-01-07T15:30:00+05:30"));

        var body = await response.ReadAsync<AvailabilityWire>();
        Assert.Equal(DateTimeOffset.Parse("2030-01-07T10:00:00Z"), body.StartAt);
        Assert.Equal(TimeSpan.Zero, body.StartAt.Offset);
        Assert.Equal(DateTimeOffset.Parse("2030-01-07T11:00:00Z"), body.EndAt);
    }

    [Fact]
    public async Task Availability_for_unknown_dealership_or_service_type_is_404_and_empty_ids_are_422()
    {
        var s = await SeedAsync();

        var unknownDealership = await PostAsync("/api/availability", new { dealershipId = Guid.NewGuid(), s.ServiceTypeId, startAt = At(10) });
        var unknownService = await PostAsync("/api/availability", new { s.DealershipId, serviceTypeId = Guid.NewGuid(), startAt = At(10) });
        var emptyIds = await PostAsync("/api/availability", new { startAt = At(10) });

        Assert.Equal(HttpStatusCode.NotFound, unknownDealership.StatusCode);
        Assert.Equal("not_found", (await unknownDealership.ReadProblemAsync()).Code);
        Assert.Equal(HttpStatusCode.NotFound, unknownService.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, emptyIds.StatusCode);
        Assert.Equal("validation_failed", (await emptyIds.ReadProblemAsync()).Code);
    }

    // ---------- booking ----------

    [Fact]
    public async Task Booking_returns_201_and_persists_the_appointment_with_its_idempotency_record()
    {
        var s = await SeedAsync();

        var response = await PostAsync("/api/appointments", s.BookingBody(At(10)), NewKey());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.ReadAsync<AppointmentWire>();
        Assert.Equal("Confirmed", created.Status);
        Assert.Equal(s.CustomerId, created.CustomerId);
        Assert.Equal(DateTimeOffset.Parse(At(10)), created.StartAt);
        Assert.Equal(DateTimeOffset.Parse(At(11)), created.EndAt);
        Assert.EndsWith($"/api/appointments/{created.Id}", response.Headers.Location!.ToString());

        var fetched = await Client.GetAsync(response.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
        Assert.Equal(created, await fetched.ReadAsync<AppointmentWire>());
        Assert.Equal(1, await AppointmentCountAsync());
        Assert.Equal(1, await RecordCountAsync());
    }

    [Fact]
    public async Task A_start_time_with_an_offset_is_stored_as_the_same_utc_instant()
    {
        var s = await SeedAsync();

        var response = await PostAsync("/api/appointments", s.BookingBody("2030-01-07T15:30:00+05:30"), NewKey());

        var created = await response.ReadAsync<AppointmentWire>();
        Assert.Equal(DateTimeOffset.Parse("2030-01-07T10:00:00Z"), created.StartAt);
        var stored = await Factory.WithDbAsync(db => Task.FromResult(db.Appointments.Single()));
        Assert.Equal(TimeSpan.Zero, stored.StartAt.Offset);
        Assert.Equal(DateTimeOffset.Parse("2030-01-07T10:00:00Z"), stored.StartAt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Booking_without_a_usable_idempotency_key_is_422_and_saves_nothing(string? key)
    {
        var s = await SeedAsync();

        var response = await PostAsync("/api/appointments", s.BookingBody(At(10)), key);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("validation_failed", (await response.ReadProblemAsync()).Code);
        Assert.Equal(0, await AppointmentCountAsync());
    }

    [Fact]
    public async Task Booking_with_an_unknown_reference_is_404()
    {
        var s = await SeedAsync();
        object Body(Guid? customer = null, Guid? vehicle = null, Guid? dealership = null, Guid? service = null) => new
        {
            customerId = customer ?? s.CustomerId,
            vehicleId = vehicle ?? s.VehicleId,
            dealershipId = dealership ?? s.DealershipId,
            serviceTypeId = service ?? s.ServiceTypeId,
            startAt = At(10)
        };

        foreach (var body in new[]
                 {
                     Body(customer: Guid.NewGuid()), Body(vehicle: Guid.NewGuid()),
                     Body(dealership: Guid.NewGuid()), Body(service: Guid.NewGuid())
                 })
        {
            var response = await PostAsync("/api/appointments", body, NewKey());
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal("not_found", (await response.ReadProblemAsync()).Code);
        }

        Assert.Equal(0, await AppointmentCountAsync());
    }

    [Fact]
    public async Task Booking_a_vehicle_that_belongs_to_another_customer_is_422()
    {
        var s = await SeedAsync();
        var other = new Customer(Guid.NewGuid(), "Other", "other@example.com");
        await Factory.WithDbAsync(async db => { db.Add(other); await db.SaveChangesAsync(); return 0; });

        var response = await PostAsync("/api/appointments", s.BookingBody(At(10), customerId: other.Id), NewKey());

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("validation_failed", (await response.ReadProblemAsync()).Code);
    }

    [Fact]
    public async Task Booking_outside_working_hours_is_422()
    {
        var s = await SeedAsync();

        var response = await PostAsync("/api/appointments", s.BookingBody(At(7)), NewKey());

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(0, await AppointmentCountAsync());
    }

    [Fact]
    public async Task Booking_when_no_technician_and_bay_are_free_is_409_and_leaves_the_first_booking_intact()
    {
        var s = await SeedAsync(technicians: 1, bays: 1);
        await PostAsync("/api/appointments", s.BookingBody(At(10)), NewKey());

        var response = await PostAsync("/api/appointments", s.BookingBody(At(10, 30)), NewKey());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("resource_unavailable", (await response.ReadProblemAsync()).Code);
        Assert.Equal(1, await AppointmentCountAsync());
        Assert.Equal(1, await RecordCountAsync()); // the failed attempt did not burn a key
    }

    [Fact]
    public async Task Back_to_back_bookings_on_the_same_single_resources_are_allowed()
    {
        var s = await SeedAsync(technicians: 1, bays: 1);

        var first = await PostAsync("/api/appointments", s.BookingBody(At(10)), NewKey());
        var second = await PostAsync("/api/appointments", s.BookingBody(At(11)), NewKey());

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
    }

    // ---------- duplicates ----------

    [Fact]
    public async Task Repeating_a_booking_with_the_same_key_returns_the_same_appointment_without_a_duplicate()
    {
        var s = await SeedAsync();
        var key = NewKey();

        var first = await PostAsync("/api/appointments", s.BookingBody(At(10)), key);
        var second = await PostAsync("/api/appointments", s.BookingBody(At(10)), key);

        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.Equal(await first.ReadAsync<AppointmentWire>(), await second.ReadAsync<AppointmentWire>());
        Assert.Equal(1, await AppointmentCountAsync());
        Assert.Equal(1, await RecordCountAsync());
    }

    [Fact]
    public async Task The_same_instant_in_another_offset_counts_as_the_same_request()
    {
        var s = await SeedAsync();
        var key = NewKey();
        var first = await PostAsync("/api/appointments", s.BookingBody("2030-01-07T10:00:00Z"), key);

        var second = await PostAsync("/api/appointments", s.BookingBody("2030-01-07T15:30:00+05:30"), key);

        Assert.Equal((await first.ReadAsync<AppointmentWire>()).Id, (await second.ReadAsync<AppointmentWire>()).Id);
        Assert.Equal(1, await AppointmentCountAsync());
    }

    [Fact]
    public async Task The_same_key_with_a_different_request_is_422_and_changes_nothing()
    {
        var s = await SeedAsync();
        var key = NewKey();
        await PostAsync("/api/appointments", s.BookingBody(At(10)), key);

        var response = await PostAsync("/api/appointments", s.BookingBody(At(14)), key);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("idempotency_key_reused", (await response.ReadProblemAsync()).Code);
        Assert.Equal(1, await AppointmentCountAsync());
    }

    // ---------- retrieval & cancellation ----------

    [Fact]
    public async Task Getting_an_unknown_appointment_is_404()
    {
        await SeedAsync();

        var response = await Client.GetAsync($"/api/appointments/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("not_found", (await response.ReadProblemAsync()).Code);
    }

    [Fact]
    public async Task Cancelling_persists_the_status_and_its_idempotency_record_and_is_visible_on_get()
    {
        var s = await SeedAsync();
        var booked = await (await PostAsync("/api/appointments", s.BookingBody(At(10)), NewKey())).ReadAsync<AppointmentWire>();

        var response = await PostAsync($"/api/appointments/{booked.Id}/cancel", null, NewKey());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cancelled = await response.ReadAsync<AppointmentWire>();
        Assert.Equal("Cancelled", cancelled.Status);
        Assert.NotNull(cancelled.CancelledAt);
        Assert.Equal(cancelled, await (await Client.GetAsync($"/api/appointments/{booked.Id}")).ReadAsync<AppointmentWire>());
        Assert.Equal(1, await AppointmentCountAsync(AppointmentStatus.Cancelled));
        Assert.Equal(2, await RecordCountAsync()); // booking + cancellation
    }

    [Fact]
    public async Task Cancelling_an_unknown_appointment_is_404_and_without_a_key_is_422()
    {
        var s = await SeedAsync();
        var booked = await (await PostAsync("/api/appointments", s.BookingBody(At(10)), NewKey())).ReadAsync<AppointmentWire>();

        var unknown = await PostAsync($"/api/appointments/{Guid.NewGuid()}/cancel", null, NewKey());
        var noKey = await PostAsync($"/api/appointments/{booked.Id}/cancel", null);

        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, noKey.StatusCode);
        Assert.Equal(1, await AppointmentCountAsync(AppointmentStatus.Confirmed));
    }

    [Fact]
    public async Task Cancelling_twice_is_409_with_a_new_key_and_a_replay_with_the_same_key()
    {
        var s = await SeedAsync();
        var booked = await (await PostAsync("/api/appointments", s.BookingBody(At(10)), NewKey())).ReadAsync<AppointmentWire>();
        var key = NewKey();
        var first = await PostAsync($"/api/appointments/{booked.Id}/cancel", null, key);

        var replay = await PostAsync($"/api/appointments/{booked.Id}/cancel", null, key);
        var again = await PostAsync($"/api/appointments/{booked.Id}/cancel", null, NewKey());

        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal(await first.ReadAsync<AppointmentWire>(), await replay.ReadAsync<AppointmentWire>());
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("invalid_state_transition", (await again.ReadProblemAsync()).Code);
    }

    [Fact]
    public async Task A_cancelled_slot_can_be_booked_again_straight_away()
    {
        var s = await SeedAsync(technicians: 1, bays: 1);
        var booked = await (await PostAsync("/api/appointments", s.BookingBody(At(10)), NewKey())).ReadAsync<AppointmentWire>();
        Assert.Equal(HttpStatusCode.Conflict, (await PostAsync("/api/appointments", s.BookingBody(At(10)), NewKey())).StatusCode);

        await PostAsync($"/api/appointments/{booked.Id}/cancel", null, NewKey());

        Assert.True(await IsAvailableAsync(s, At(10)));
        var rebooked = await PostAsync("/api/appointments", s.BookingBody(At(10)), NewKey());
        Assert.Equal(HttpStatusCode.Created, rebooked.StatusCode);
        var again = await rebooked.ReadAsync<AppointmentWire>();
        Assert.NotEqual(booked.Id, again.Id);
        Assert.Equal((booked.TechnicianId, booked.ServiceBayId), (again.TechnicianId, again.ServiceBayId));
    }

    [Fact]
    public async Task Replaying_a_booking_after_it_was_cancelled_returns_the_original_response()
    {
        var s = await SeedAsync();
        var key = NewKey();
        var booked = await (await PostAsync("/api/appointments", s.BookingBody(At(10)), key)).ReadAsync<AppointmentWire>();
        await PostAsync($"/api/appointments/{booked.Id}/cancel", null, NewKey());

        var replay = await (await PostAsync("/api/appointments", s.BookingBody(At(10)), key)).ReadAsync<AppointmentWire>();

        Assert.Equal(booked, replay);
        Assert.Equal("Confirmed", replay.Status);
    }

    private Task<Scenario> SeedAsync(int technicians = 2, int bays = 2) => Scenario.SeedAsync(Factory, technicians, bays);

    private async Task<bool> IsAvailableAsync(Scenario s, string startAt)
    {
        var response = await PostAsync("/api/availability", s.AvailabilityBody(startAt));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.ReadAsync<AvailabilityWire>()).IsAvailable;
    }
}
