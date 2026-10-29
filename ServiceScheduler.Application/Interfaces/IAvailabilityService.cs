using ServiceScheduler.Application.DTOs;

namespace ServiceScheduler.Application.Interfaces;

public interface IAvailabilityService
{
    /// <summary>Read-only: reports whether a technician and bay are free for the whole service; reserves nothing.</summary>
    Task<AvailabilityResult> CheckAsync(AvailabilityRequest request, CancellationToken cancellationToken = default);
}
