using System.Net;
using ServiceScheduler.Application.DTOs;

namespace ServiceScheduler.Api.Tests;

public class AppointmentEndpointTests : IClassFixture<StubbedApiFactory>
{
    private readonly StubbedApiFactory _factory;
    private readonly HttpClient _client;

    public AppointmentEndpointTests(StubbedApiFactory factory)
    {
        _factory = factory;
        _factory.Booking.Reset();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Booking_returns_201_with_a_location_and_the_appointment()
    {
        var created = Sample.Appointment();
        _factory.Booking.OnBook = (_, _) => created;

        var response = await _client.SendAsync(Http.Post("/api/appointments", Http.BookingBody("2030-01-07T10:00:00Z"), "key-1"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.EndsWith($"/api/appointments/{created.Id}", response.Headers.Location!.ToString());
        var body = await response.ReadAsync<AppointmentWire>();
        Assert.Equal(created.Id, body.Id);
        Assert.Equal(created.TechnicianId, body.TechnicianId);
        Assert.Equal(created.ServiceBayId, body.ServiceBayId);
        Assert.Equal("Confirmed", body.Status);
        Assert.Equal(Sample.Start, body.StartAt);
        Assert.Equal(Sample.Start.AddHours(1), body.EndAt);
    }

    [Fact]
    public async Task Booking_passes_the_idempotency_key_and_request_to_the_service()
    {
        _factory.Booking.OnBook = (_, _) => Sample.Appointment();
        var customerId = Guid.NewGuid();

        await _client.SendAsync(Http.Post("/api/appointments", Http.BookingBody("2030-01-07T15:30:00+05:30", customerId), "key-abc-123"));

        Assert.Equal("key-abc-123", _factory.Booking.LastKey);
        Assert.Equal(customerId, _factory.Booking.LastBookRequest!.CustomerId);
        Assert.Equal(Sample.Start, _factory.Booking.LastBookRequest.StartAt);
    }

    [Fact]
    public async Task Booking_without_the_header_reaches_the_service_with_a_blank_key_so_the_application_rejects_it()
    {
        _factory.Booking.OnBook = (_, _) => Sample.Appointment();

        await _client.SendAsync(Http.Post("/api/appointments", Http.BookingBody("2030-01-07T10:00:00Z")));

        Assert.Equal(1, _factory.Booking.Calls);
        Assert.True(string.IsNullOrWhiteSpace(_factory.Booking.LastKey));
    }

    [Theory]
    [InlineData("2030-01-07T10:00:00")]
    [InlineData("garbage")]
    [InlineData("")]
    public async Task Booking_rejects_a_start_without_offset_with_400_and_never_calls_the_service(string startAt)
    {
        var response = await _client.SendAsync(Http.Post("/api/appointments", Http.BookingBody(startAt), "key-1"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("bad_request", (await response.ReadProblemAsync()).Code);
        Assert.Equal(0, _factory.Booking.Calls);
    }

    [Fact]
    public async Task Get_returns_200_with_the_appointment()
    {
        var existing = Sample.Appointment();
        _factory.Booking.OnGet = _ => existing;

        var response = await _client.GetAsync($"/api/appointments/{existing.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(existing.Id, (await response.ReadAsync<AppointmentWire>()).Id);
        Assert.Equal(existing.Id, _factory.Booking.LastAppointmentId);
    }

    [Fact]
    public async Task Get_does_not_need_an_idempotency_key()
    {
        _factory.Booking.OnGet = _ => Sample.Appointment();

        var response = await _client.GetAsync($"/api/appointments/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task A_route_id_that_is_not_a_guid_is_404_and_never_reaches_the_service()
    {
        var get = await _client.GetAsync("/api/appointments/not-a-guid");
        var cancel = await _client.SendAsync(Http.Post("/api/appointments/not-a-guid/cancel", null, "k"));

        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, cancel.StatusCode);
        Assert.Equal(0, _factory.Booking.Calls);
    }

    [Fact]
    public async Task Cancel_returns_200_with_the_cancelled_appointment_and_passes_key_and_id()
    {
        var id = Guid.NewGuid();
        _factory.Booking.OnCancel = (_, appointmentId) => Sample.Appointment(appointmentId, "Cancelled");

        var response = await _client.SendAsync(Http.Post($"/api/appointments/{id}/cancel", null, "cancel-key"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.ReadAsync<AppointmentWire>();
        Assert.Equal("Cancelled", body.Status);
        Assert.NotNull(body.CancelledAt);
        Assert.Equal("cancel-key", _factory.Booking.LastKey);
        Assert.Equal(id, _factory.Booking.LastAppointmentId);
    }

    [Fact]
    public async Task Cancel_without_the_header_reaches_the_service_with_a_blank_key()
    {
        _factory.Booking.OnCancel = (_, id) => Sample.Appointment(id, "Cancelled");

        await _client.SendAsync(Http.Post($"/api/appointments/{Guid.NewGuid()}/cancel", null));

        Assert.Equal(1, _factory.Booking.Calls);
        Assert.True(string.IsNullOrWhiteSpace(_factory.Booking.LastKey));
    }
}
