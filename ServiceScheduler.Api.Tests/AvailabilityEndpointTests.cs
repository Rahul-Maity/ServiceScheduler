using System.Net;
using System.Net.Http.Json;
using ServiceScheduler.Application.DTOs;

namespace ServiceScheduler.Api.Tests;

public class AvailabilityEndpointTests : IClassFixture<StubbedApiFactory>
{
    private readonly StubbedApiFactory _factory;
    private readonly HttpClient _client;

    public AvailabilityEndpointTests(StubbedApiFactory factory)
    {
        _factory = factory;
        _factory.Availability.Reset();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Returns_200_with_the_result_from_the_service()
    {
        _factory.Availability.OnCheck = r => new AvailabilityResult(true, r.StartAt.ToUniversalTime(), r.StartAt.ToUniversalTime().AddHours(1));

        var response = await _client.SendAsync(Http.Post("/api/availability", Http.AvailabilityBody("2030-01-07T10:00:00Z")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.ReadAsync<AvailabilityWire>();
        Assert.True(body.IsAvailable);
        Assert.Equal(Sample.Start, body.StartAt);
        Assert.Equal(Sample.Start.AddHours(1), body.EndAt);
    }

    [Fact]
    public async Task Reports_unavailable_as_200_not_as_an_error()
    {
        _factory.Availability.OnCheck = r => new AvailabilityResult(false, r.StartAt, r.StartAt.AddHours(1));

        var response = await _client.SendAsync(Http.Post("/api/availability", Http.AvailabilityBody("2030-01-07T10:00:00Z")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False((await response.ReadAsync<AvailabilityWire>()).IsAvailable);
    }

    [Fact]
    public async Task Passes_the_ids_through_to_the_service()
    {
        _factory.Availability.OnCheck = r => new AvailabilityResult(true, r.StartAt, r.StartAt.AddHours(1));
        var dealershipId = Guid.NewGuid();
        var serviceTypeId = Guid.NewGuid();

        await _client.SendAsync(Http.Post("/api/availability", new { dealershipId, serviceTypeId, startAt = "2030-01-07T10:00:00Z" }));

        Assert.Equal(dealershipId, _factory.Availability.LastRequest!.DealershipId);
        Assert.Equal(serviceTypeId, _factory.Availability.LastRequest.ServiceTypeId);
    }

    [Theory]
    [InlineData("2030-01-07T10:00:00Z", "2030-01-07T10:00:00Z")]
    [InlineData("2030-01-07T10:00Z", "2030-01-07T10:00:00Z")]
    [InlineData("2030-01-07T15:30:00+05:30", "2030-01-07T10:00:00Z")]
    [InlineData("2030-01-07T02:00:00-08:00", "2030-01-07T10:00:00Z")]
    [InlineData("2030-01-07T10:00:00.5Z", "2030-01-07T10:00:00.5Z")]
    [InlineData("2030-01-07T10:00:00.1234567Z", "2030-01-07T10:00:00.123456Z")] // PostgreSQL keeps microseconds; so do we
    public async Task Accepts_timestamps_with_an_offset_and_hands_the_same_instant_to_the_service(string sent, string instant)
    {
        _factory.Availability.OnCheck = r => new AvailabilityResult(true, r.StartAt, r.StartAt.AddHours(1));

        var response = await _client.SendAsync(Http.Post("/api/availability", Http.AvailabilityBody(sent)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(DateTimeOffset.Parse(instant), _factory.Availability.LastRequest!.StartAt);
    }

    [Theory]
    [InlineData("2030-01-07T10:00:00")]        // no offset
    [InlineData("2030-01-07T10:00")]
    [InlineData("2030-01-07")]
    [InlineData("10:00")]
    [InlineData("not a date")]
    [InlineData("")]
    [InlineData("2030-01-07T25:00:00Z")]       // has an offset but is not a real time
    public async Task Rejects_missing_offset_or_unparsable_timestamps_with_400_and_never_calls_the_service(string sent)
    {
        var response = await _client.SendAsync(Http.Post("/api/availability", Http.AvailabilityBody(sent)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.ReadProblemAsync();
        Assert.Equal(400, problem.Status);
        Assert.Equal("bad_request", problem.Code);
        Assert.Equal(0, _factory.Availability.Calls);
    }

    [Fact]
    public async Task Rejects_a_missing_start_with_400()
    {
        var response = await _client.SendAsync(Http.Post("/api/availability", new { dealershipId = Guid.NewGuid(), serviceTypeId = Guid.NewGuid() }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, _factory.Availability.Calls);
    }

    [Fact]
    public async Task Rejects_malformed_json_and_an_empty_body_with_a_problem_response()
    {
        var malformed = await _client.PostAsync("/api/availability",
            new StringContent("{ not json", System.Text.Encoding.UTF8, "application/json"));
        var empty = await _client.SendAsync(Http.Post("/api/availability", null));

        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
        Assert.Equal("bad_request", (await malformed.ReadProblemAsync()).Code);
        Assert.True(empty.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.UnsupportedMediaType);
        Assert.Equal(0, _factory.Availability.Calls);
    }

    [Fact]
    public async Task Does_not_require_an_idempotency_key()
    {
        _factory.Availability.OnCheck = r => new AvailabilityResult(true, r.StartAt, r.StartAt.AddHours(1));

        var response = await _client.PostAsJsonAsync("/api/availability", Http.AvailabilityBody("2030-01-07T10:00:00Z"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
