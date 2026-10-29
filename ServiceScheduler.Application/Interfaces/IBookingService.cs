using ServiceScheduler.Application.DTOs;

namespace ServiceScheduler.Application.Interfaces;

public interface IBookingService
{
    Task<AppointmentDto> BookAsync(string idempotencyKey, BookAppointmentRequest request, CancellationToken cancellationToken = default);

    Task<AppointmentDto> CancelAsync(string idempotencyKey, Guid appointmentId, CancellationToken cancellationToken = default);

    Task<AppointmentDto> GetAsync(Guid appointmentId, CancellationToken cancellationToken = default);
}
