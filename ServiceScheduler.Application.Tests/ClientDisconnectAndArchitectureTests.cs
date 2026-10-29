using ServiceScheduler.Application.Services;
using static ServiceScheduler.Application.Tests.World;

namespace ServiceScheduler.Application.Tests;

public class ClientDisconnectAndArchitectureTests
{
    private readonly World _w = new();

    [Fact]
    public async Task Token_cancelled_before_the_work_starts_does_nothing()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _w.NewBooking().BookAsync(Key(), _w.Request(At(10)), cts.Token));

        Assert.Equal(0, _w.Store.SaveCalls);
        Assert.Empty(_w.Store.Appointments);
    }

    [Fact]
    public async Task Token_cancelled_after_candidate_selection_but_before_commit_saves_nothing()
    {
        using var cts = new CancellationTokenSource();
        _w.Store.OnResourceQuery = () => cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _w.NewBooking().BookAsync(Key(), _w.Request(At(10)), cts.Token));

        Assert.Equal(0, _w.Store.SaveCalls);
        Assert.Empty(_w.Store.Appointments);
        Assert.Empty(_w.Store.Records);
    }

    [Fact]
    public async Task The_commit_itself_is_not_abortable_so_a_confirmed_booking_stays_confirmed()
    {
        using var cts = new CancellationTokenSource();

        await _w.NewBooking().BookAsync(Key(), _w.Request(At(10)), cts.Token);

        Assert.False(_w.Store.SaveTokens.Single().CanBeCanceled);
    }

    [Fact]
    public void Application_references_only_the_domain()
    {
        var referenced = typeof(BookingService).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToList();

        Assert.Contains("ServiceScheduler.Domain", referenced);
        Assert.DoesNotContain("ServiceScheduler.Infrastructure", referenced);
        Assert.DoesNotContain("ServiceScheduler.Api", referenced);
        Assert.DoesNotContain(referenced, n => n!.StartsWith("Microsoft.EntityFrameworkCore") || n.StartsWith("Npgsql"));
    }
}
