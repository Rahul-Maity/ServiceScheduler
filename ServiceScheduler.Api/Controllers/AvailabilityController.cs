using Microsoft.AspNetCore.Mvc;
using ServiceScheduler.Api.Contracts;
using ServiceScheduler.Application.DTOs;
using ServiceScheduler.Application.Interfaces;

namespace ServiceScheduler.Api.Controllers;

[ApiController]
[Route("api/availability")]
public sealed class AvailabilityController(IAvailabilityService availability) : ControllerBase
{
    /// <summary>Checks whether a qualified technician and a compatible bay are free for the whole service. Reserves nothing.</summary>
    [HttpPost]
    [ProducesResponseType<AvailabilityResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<AvailabilityResponse>> Check(CheckAvailabilityRequest request, CancellationToken cancellationToken)
    {
        var startAt = OffsetTimestamp.Parse(request.StartAt, nameof(request.StartAt));

        var result = await availability.CheckAsync(
            new AvailabilityRequest(request.DealershipId, request.ServiceTypeId, startAt), cancellationToken);

        return Ok(result.ToResponse());
    }
}
