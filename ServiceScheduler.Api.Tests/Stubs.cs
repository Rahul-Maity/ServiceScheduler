using ServiceScheduler.Application.DTOs;
using ServiceScheduler.Application.Interfaces;

namespace ServiceScheduler.Api.Tests;

/// <summary>Programmable stand-in for the booking service; records what the controllers pass in.</summary>
public sealed class StubBookingService : IBookingService
{
    public Func<string, BookAppointmentRequest, AppointmentDto> OnBook { get; set; } = (_, _) => throw new NotImplementedException();
    public Func<string, Guid, AppointmentDto> OnCancel { get; set; } = (_, _) => throw new NotImplementedException();
    public Func<Guid, AppointmentDto> OnGet { get; set; } = _ => throw new NotImplementedException();

    public int Calls { get; private set; }
    public string? LastKey { get; private set; }
    public BookAppointmentRequest? LastBookRequest { get; private set; }
    public Guid? LastAppointmentId { get; private set; }

    public void Reset()
    {
        OnBook = (_, _) => throw new NotImplementedException();
        OnCancel = (_, _) => throw new NotImplementedException();
        OnGet = _ => throw new NotImplementedException();
        Calls = 0;
        LastKey = null;
        LastBookRequest = null;
        LastAppointmentId = null;
    }

    public Task<AppointmentDto> BookAsync(string idempotencyKey, BookAppointmentRequest request, CancellationToken cancellationToken = default)
    {
        Calls++;
        LastKey = idempotencyKey;
        LastBookRequest = request;
        return Task.FromResult(OnBook(idempotencyKey, request));
    }

    public Task<AppointmentDto> CancelAsync(string idempotencyKey, Guid appointmentId, CancellationToken cancellationToken = default)
    {
        Calls++;
        LastKey = idempotencyKey;
        LastAppointmentId = appointmentId;
        return Task.FromResult(OnCancel(idempotencyKey, appointmentId));
    }

    public Task<AppointmentDto> GetAsync(Guid appointmentId, CancellationToken cancellationToken = default)
    {
        Calls++;
        LastAppointmentId = appointmentId;
        return Task.FromResult(OnGet(appointmentId));
    }
}

public sealed class StubAvailabilityService : IAvailabilityService
{
    public Func<AvailabilityRequest, AvailabilityResult> OnCheck { get; set; } = _ => throw new NotImplementedException();

    public int Calls { get; private set; }
    public AvailabilityRequest? LastRequest { get; private set; }

    public void Reset()
    {
        OnCheck = _ => throw new NotImplementedException();
        Calls = 0;
        LastRequest = null;
    }

    public Task<AvailabilityResult> CheckAsync(AvailabilityRequest request, CancellationToken cancellationToken = default)
    {
        Calls++;
        LastRequest = request;
        return Task.FromResult(OnCheck(request));
    }
}

public static class Sample
{
    public static readonly DateTimeOffset Start = new(2030, 1, 7, 10, 0, 0, TimeSpan.Zero);

    public static AppointmentDto Appointment(Guid? id = null, string status = "Confirmed") => new(
        id ?? Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
        Start, Start.AddHours(1), status, Start.AddDays(-30), status == "Cancelled" ? Start.AddDays(-1) : null);
}
