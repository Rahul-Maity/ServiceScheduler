using System.Net;
using ServiceScheduler.Domain.Exceptions;

namespace ServiceScheduler.Api.Tests;

public class ErrorHandlingTests : IClassFixture<StubbedApiFactory>
{
    private readonly StubbedApiFactory _factory;
    private readonly HttpClient _client;

    public ErrorHandlingTests(StubbedApiFactory factory)
    {
        _factory = factory;
        _factory.Booking.Reset();
        _client = factory.CreateClient();
    }

    private static Exception Make(string kind) => kind switch
    {
        "validation" => new DomainValidationException("The requested time is outside the dealership's working hours."),
        "idempotency" => new IdempotencyConflictException(),
        "not_found" => new EntityNotFoundException("Appointment", Guid.NewGuid()),
        "unavailable" => new ResourceUnavailableException("No technician and service bay are available for the requested time."),
        "transition" => new InvalidStateTransitionException("Cancelled", "Cancelled"),
        "duplicate" => new DuplicateIdempotencyKeyException(),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    [Theory]
    [InlineData("validation", 422, "validation_failed")]
    [InlineData("idempotency", 422, "idempotency_key_reused")]
    [InlineData("not_found", 404, "not_found")]
    [InlineData("unavailable", 409, "resource_unavailable")]
    [InlineData("transition", 409, "invalid_state_transition")]
    [InlineData("duplicate", 409, "duplicate_request")]
    public async Task Domain_exceptions_become_problem_responses_with_the_right_status_and_code(string kind, int status, string code)
    {
        var exception = Make(kind);
        _factory.Booking.OnGet = _ => throw exception;

        var response = await _client.GetAsync($"/api/appointments/{Guid.NewGuid()}");

        Assert.Equal(status, (int)response.StatusCode);
        var problem = await response.ReadProblemAsync();
        Assert.Equal(status, problem.Status);
        Assert.Equal(code, problem.Code);
        Assert.Equal(exception.Message, problem.Detail);
        Assert.False(string.IsNullOrWhiteSpace(problem.Title));
    }

    [Fact]
    public async Task Errors_from_booking_and_cancellation_use_the_same_mapping()
    {
        _factory.Booking.OnBook = (_, _) => throw new ResourceUnavailableException("none free");
        _factory.Booking.OnCancel = (_, _) => throw new InvalidStateTransitionException("Cancelled", "Cancelled");

        var book = await _client.SendAsync(Http.Post("/api/appointments", Http.BookingBody("2030-01-07T10:00:00Z"), "k1"));
        var cancel = await _client.SendAsync(Http.Post($"/api/appointments/{Guid.NewGuid()}/cancel", null, "k2"));

        Assert.Equal(HttpStatusCode.Conflict, book.StatusCode);
        Assert.Equal("resource_unavailable", (await book.ReadProblemAsync()).Code);
        Assert.Equal(HttpStatusCode.Conflict, cancel.StatusCode);
        Assert.Equal("invalid_state_transition", (await cancel.ReadProblemAsync()).Code);
    }

    [Fact]
    public async Task Unexpected_failures_are_500_and_do_not_leak_internal_details()
    {
        _factory.Booking.OnGet = _ => throw new InvalidOperationException("secret connection string Password=hunter2");

        var response = await _client.GetAsync($"/api/appointments/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("hunter2", raw);
        Assert.DoesNotContain("InvalidOperationException", raw);
        var problem = System.Text.Json.JsonSerializer.Deserialize<ProblemWire>(raw, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;
        Assert.Equal(500, problem.Status);
        Assert.Equal("internal_error", problem.Code);
    }

    [Fact]
    public async Task Problem_responses_carry_the_correlation_id_from_the_response_header()
    {
        _factory.Booking.OnGet = _ => throw new EntityNotFoundException("Appointment", Guid.NewGuid());

        var response = await _client.GetAsync($"/api/appointments/{Guid.NewGuid()}");

        var problem = await response.ReadProblemAsync();
        Assert.False(string.IsNullOrWhiteSpace(problem.CorrelationId));
        Assert.Equal(response.Headers.GetValues("X-Correlation-Id").Single(), problem.CorrelationId);
    }

    [Fact]
    public async Task Request_body_errors_use_the_same_problem_shape_with_a_correlation_id()
    {
        var response = await _client.PostAsync("/api/availability",
            new StringContent("{ not json", System.Text.Encoding.UTF8, "application/json"));

        var problem = await response.ReadProblemAsync();
        Assert.Equal("bad_request", problem.Code);
        Assert.False(string.IsNullOrWhiteSpace(problem.CorrelationId));
    }
}
