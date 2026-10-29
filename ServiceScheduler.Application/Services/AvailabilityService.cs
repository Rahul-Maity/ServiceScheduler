using ServiceScheduler.Application.DTOs;
using ServiceScheduler.Application.Interfaces;
using ServiceScheduler.Application.Validators;
using ServiceScheduler.Domain.Exceptions;
using ServiceScheduler.Domain.Interfaces;

namespace ServiceScheduler.Application.Services;

public sealed class AvailabilityService(
    IDealershipRepository dealerships,
    IServiceTypeRepository serviceTypes,
    ResourceSelector selector) : IAvailabilityService
{
    public async Task<AvailabilityResult> CheckAsync(AvailabilityRequest request, CancellationToken cancellationToken = default)
    {
        RequestValidator.Validate(request);

        var dealership = await dealerships.GetByIdAsync(request.DealershipId, cancellationToken)
                         ?? throw new EntityNotFoundException("Dealership", request.DealershipId);
        var serviceType = await serviceTypes.GetByIdAsync(request.ServiceTypeId, cancellationToken)
                          ?? throw new EntityNotFoundException("Service type", request.ServiceTypeId);

        var startAt = request.StartAt.ToUniversalTime();
        var endAt = startAt + serviceType.Duration;

        var isAvailable = dealership.IsOpenDuring(startAt, endAt)
                          && (await selector.FindPairsAsync(dealership, serviceType, startAt, endAt, cancellationToken)).Count > 0;

        return new AvailabilityResult(isAvailable, startAt, endAt);
    }
}
