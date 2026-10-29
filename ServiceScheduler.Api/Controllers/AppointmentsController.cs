using Microsoft.AspNetCore.Mvc;
using ServiceScheduler.Api.Contracts;
using ServiceScheduler.Api.Middleware;
using ServiceScheduler.Application.DTOs;
using ServiceScheduler.Application.Interfaces;

namespace ServiceScheduler.Api.Controllers;

[ApiController]
[Route("api/appointments")]
public sealed class AppointmentsController(IBookingService booking, IIdempotencyKeyAccessor idempotencyKey) : ControllerBase
{
    /// <summary>Books an appointment. Repeating the request with the same Idempotency-Key returns the original appointment.</summary>
    [HttpPost]
    [RequiresIdempotencyKey]
    [ProducesResponseType<AppointmentResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<AppointmentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<AppointmentResponse>> Create(CreateAppointmentRequest request, CancellationToken cancellationToken)
    {
        var startAt = OffsetTimestamp.Parse(request.StartAt, nameof(request.StartAt));

        var appointment = await booking.BookAsync(
            idempotencyKey.Key ?? string.Empty,
            new BookAppointmentRequest(request.CustomerId, request.VehicleId, request.DealershipId, request.ServiceTypeId, startAt),
            cancellationToken);

        var response = appointment.ToResponse();
        if (appointment.IsReplay)
        {
            return Ok(response with { Message = "This appointment is already booked. Returning the original booking for this Idempotency-Key." });
        }

        return CreatedAtAction(nameof(Get), new { id = appointment.Id }, response);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<AppointmentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AppointmentResponse>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok((await booking.GetAsync(id, cancellationToken)).ToResponse());

    /// <summary>Cancels a confirmed appointment and releases its technician and bay.</summary>
    [HttpPost("{id:guid}/cancel")]
    [RequiresIdempotencyKey]
    [ProducesResponseType<AppointmentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<AppointmentResponse>> Cancel(Guid id, CancellationToken cancellationToken) =>
        Ok((await booking.CancelAsync(idempotencyKey.Key ?? string.Empty, id, cancellationToken)).ToResponse());
}
