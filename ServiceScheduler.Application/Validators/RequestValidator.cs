using ServiceScheduler.Application.DTOs;
using ServiceScheduler.Domain.Exceptions;

namespace ServiceScheduler.Application.Validators;

/// <summary>Input validation. Throws <see cref="DomainValidationException"/> on the first problem.</summary>
public static class RequestValidator
{
    /// <summary>Matches the idempotency_records.key column.</summary>
    public const int MaxIdempotencyKeyLength = 255;

    public static void Validate(AvailabilityRequest request)
    {
        RequireId(request.DealershipId, nameof(request.DealershipId));
        RequireId(request.ServiceTypeId, nameof(request.ServiceTypeId));
        RequireStart(request.StartAt);
    }

    public static void Validate(BookAppointmentRequest request)
    {
        RequireId(request.CustomerId, nameof(request.CustomerId));
        RequireId(request.VehicleId, nameof(request.VehicleId));
        RequireId(request.DealershipId, nameof(request.DealershipId));
        RequireId(request.ServiceTypeId, nameof(request.ServiceTypeId));
        RequireStart(request.StartAt);
    }

    public static void ValidateIdempotencyKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new DomainValidationException("An Idempotency-Key is required.");
        }

        if (key.Length > MaxIdempotencyKeyLength)
        {
            throw new DomainValidationException($"The Idempotency-Key must be at most {MaxIdempotencyKeyLength} characters.");
        }
    }

    private static void RequireId(Guid id, string name)
    {
        if (id == Guid.Empty)
        {
            throw new DomainValidationException($"{name} is required.");
        }
    }

    private static void RequireStart(DateTimeOffset startAt)
    {
        if (startAt == default)
        {
            throw new DomainValidationException("StartAt is required.");
        }
    }
}
