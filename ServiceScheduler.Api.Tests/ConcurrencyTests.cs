using System.Net;
using static ServiceScheduler.Api.Tests.Scenario;

namespace ServiceScheduler.Api.Tests;

/// <summary>
/// Parallel HTTP requests against the real database. Where it matters, a gate inside candidate selection makes every
/// request see the slot as free before any of them commits, so only the database's exclusion constraints can decide.
/// </summary>
[Collection("Database")]
public class ConcurrencyTests(DatabaseApiFactory factory) : DatabaseTestBase(factory)
{
    [Fact]
    public async Task Requests_that_all_pass_the_availability_check_are_still_decided_by_the_database()
    {
        const int requests = 6;
        var s = await Scenario.SeedAsync(Factory, technicians: 1, bays: 1);

        // Everyone first asks "is it free?" -- and everyone is told yes.
        var checks = await Task.WhenAll(Enumerable.Range(0, requests)
            .Select(_ => PostAsync("/api/availability", s.AvailabilityBody(At(10)))));
        foreach (var check in checks)
        {
            Assert.True((await check.ReadAsync<AvailabilityWire>()).IsAvailable);
        }

        // Then everyone books. The gate holds each booking after its own candidate query until all have queried,
        // so every request has a stale "free" snapshot when the commits race.
        Factory.Gate.Require(requests);
        var responses = await Task.WhenAll(Enumerable.Range(0, requests)
            .Select(_ => PostAsync("/api/appointments", s.BookingBody(At(10)), NewKey())));

        Assert.Equal(requests, Factory.Gate.Arrived);
        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        var losers = responses.Where(r => r.StatusCode != HttpStatusCode.Created).ToList();
        Assert.Equal(requests - 1, losers.Count);
        foreach (var loser in losers)
        {
            Assert.Equal(HttpStatusCode.Conflict, loser.StatusCode);
            Assert.Equal("resource_unavailable", (await loser.ReadProblemAsync()).Code);
        }

        Assert.Equal(1, await AppointmentCountAsync());
    }

    [Fact]
    public async Task With_two_technicians_and_two_bays_exactly_two_stale_requests_win_on_disjoint_resources()
    {
        const int requests = 8;
        var s = await Scenario.SeedAsync(Factory, technicians: 2, bays: 2);

        Factory.Gate.Require(requests);
        var responses = await Task.WhenAll(Enumerable.Range(0, requests)
            .Select(_ => PostAsync("/api/appointments", s.BookingBody(At(10)), NewKey())));

        Assert.Equal(requests, Factory.Gate.Arrived);
        var winners = new List<AppointmentWire>();
        foreach (var created in responses.Where(r => r.StatusCode == HttpStatusCode.Created))
        {
            winners.Add(await created.ReadAsync<AppointmentWire>());
        }

        Assert.Equal(2, winners.Count);
        Assert.Equal(2, winners.Select(w => w.TechnicianId).Distinct().Count());
        Assert.Equal(2, winners.Select(w => w.ServiceBayId).Distinct().Count());
        Assert.All(responses.Where(r => r.StatusCode != HttpStatusCode.Created), r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
        Assert.Equal(2, await AppointmentCountAsync());
    }

    [Fact]
    public async Task Overlapping_but_not_identical_slots_never_double_book_the_same_resource()
    {
        var s = await Scenario.SeedAsync(Factory, technicians: 1, bays: 1);
        var starts = new[] { At(10), At(10, 15), At(10, 30), At(10, 45), At(10, 59) }; // all overlap each other

        var responses = await Task.WhenAll(starts.Select(start => PostAsync("/api/appointments", s.BookingBody(start), NewKey())));

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.All(responses.Where(r => r.StatusCode != HttpStatusCode.Created), r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
        Assert.Equal(1, await AppointmentCountAsync());
    }

    [Fact]
    public async Task Parallel_retries_with_the_same_key_produce_one_appointment_and_one_shared_response()
    {
        const int requests = 8;
        var s = await Scenario.SeedAsync(Factory);
        var key = NewKey();

        var responses = await Task.WhenAll(Enumerable.Range(0, requests)
            .Select(_ => PostAsync("/api/appointments", s.BookingBody(At(10)), key)));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));
        var ids = new HashSet<Guid>();
        foreach (var response in responses)
        {
            ids.Add((await response.ReadAsync<AppointmentWire>()).Id);
        }

        Assert.Single(ids);
        Assert.Equal(1, await AppointmentCountAsync());
        Assert.Equal(1, await RecordCountAsync());
    }

    [Fact]
    public async Task Cancelling_while_others_race_for_the_slot_hands_it_over_exactly_once()
    {
        var s = await Scenario.SeedAsync(Factory, technicians: 1, bays: 1);
        var booked = await (await PostAsync("/api/appointments", s.BookingBody(At(10)), NewKey())).ReadAsync<AppointmentWire>();
        await PostAsync($"/api/appointments/{booked.Id}/cancel", null, NewKey());

        var responses = await Task.WhenAll(Enumerable.Range(0, 5)
            .Select(_ => PostAsync("/api/appointments", s.BookingBody(At(10)), NewKey())));

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(1, await AppointmentCountAsync(Domain.Enums.AppointmentStatus.Confirmed));
        Assert.Equal(1, await AppointmentCountAsync(Domain.Enums.AppointmentStatus.Cancelled));
    }
}
