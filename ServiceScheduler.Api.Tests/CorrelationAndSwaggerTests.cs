using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace ServiceScheduler.Api.Tests;

public class CorrelationAndSwaggerTests : IClassFixture<StubbedApiFactory>
{
    private readonly StubbedApiFactory _factory;
    private readonly HttpClient _client;

    public CorrelationAndSwaggerTests(StubbedApiFactory factory)
    {
        _factory = factory;
        _factory.Booking.Reset();
        _factory.Booking.OnGet = _ => Sample.Appointment();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task A_supplied_correlation_id_is_echoed()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/appointments/{Guid.NewGuid()}");
        request.Headers.Add("X-Correlation-Id", "trace-me-123");

        var response = await _client.SendAsync(request);

        Assert.Equal("trace-me-123", response.Headers.GetValues("X-Correlation-Id").Single());
    }

    [Fact]
    public async Task A_correlation_id_is_generated_when_absent_and_differs_per_request()
    {
        var first = await _client.GetAsync($"/api/appointments/{Guid.NewGuid()}");
        var second = await _client.GetAsync($"/api/appointments/{Guid.NewGuid()}");

        var a = first.Headers.GetValues("X-Correlation-Id").Single();
        var b = second.Headers.GetValues("X-Correlation-Id").Single();
        Assert.False(string.IsNullOrWhiteSpace(a));
        Assert.NotEqual(a, b);
    }

    [Fact]
    public async Task Swagger_documents_the_four_operations_and_the_idempotency_header_on_the_two_that_need_it()
    {
        var response = await _client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = doc.RootElement.GetProperty("paths");
        Assert.True(paths.TryGetProperty("/api/availability", out _));
        Assert.True(paths.GetProperty("/api/appointments").TryGetProperty("post", out _));
        Assert.True(paths.GetProperty("/api/appointments/{id}").TryGetProperty("get", out _));
        Assert.True(paths.GetProperty("/api/appointments/{id}/cancel").TryGetProperty("post", out _));
        Assert.False(paths.TryGetProperty("/weatherforecast", out _));

        Assert.True(HasIdempotencyHeader(paths.GetProperty("/api/appointments").GetProperty("post")));
        Assert.True(HasIdempotencyHeader(paths.GetProperty("/api/appointments/{id}/cancel").GetProperty("post")));
        Assert.False(HasIdempotencyHeader(paths.GetProperty("/api/availability").GetProperty("post")));
        Assert.False(HasIdempotencyHeader(paths.GetProperty("/api/appointments/{id}").GetProperty("get")));
    }

    [Fact]
    public async Task Swagger_ui_is_served_in_development()
    {
        var response = await _client.GetAsync("/swagger/index.html");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public void Controllers_depend_only_on_application_service_contracts_not_on_infrastructure_or_persistence()
    {
        var controllers = typeof(Program).Assembly.GetTypes().Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract).ToList();

        Assert.True(controllers.Count >= 2, "expected the availability and appointments controllers");
        var dependencies = controllers.SelectMany(c => c.GetConstructors()).SelectMany(c => c.GetParameters()).Select(p => p.ParameterType);
        Assert.All(dependencies, type =>
        {
            Assert.DoesNotContain("Infrastructure", type.Namespace ?? "");
            Assert.DoesNotContain("EntityFrameworkCore", type.Namespace ?? "");
            Assert.DoesNotContain("ServiceScheduler.Domain.Entities", type.Namespace ?? "");
        });
    }

    private static bool HasIdempotencyHeader(JsonElement operation) =>
        operation.TryGetProperty("parameters", out var parameters)
        && parameters.EnumerateArray().Any(p =>
            p.GetProperty("name").GetString() == "Idempotency-Key"
            && p.GetProperty("in").GetString() == "header"
            && p.TryGetProperty("required", out var required) && required.GetBoolean());
}
