using System.Text.Json;
using ServiceScheduler.Application.DTOs;
using ServiceScheduler.Application.Interfaces;
using ServiceScheduler.Application.Validators;
using ServiceScheduler.Domain.Entities;
using ServiceScheduler.Domain.Exceptions;
using ServiceScheduler.Domain.Interfaces;

namespace ServiceScheduler.Application.Services;

public sealed class BookingService(
    ICustomerRepository customers,
    IVehicleRepository vehicles,
    IDealershipRepository dealerships,
    IServiceTypeRepository serviceTypes,
    IAppointmentRepository appointments,
    ResourceSelector selector,
    IIdempotencyStore idempotency,
    IUnitOfWork unitOfWork,
    IClock clock) : IBookingService
{
    public async Task<AppointmentDto> BookAsync(string idempotencyKey, BookAppointmentRequest request, CancellationToken cancellationToken = default)
    {
        RequestValidator.ValidateIdempotencyKey(idempotencyKey);
        RequestValidator.Validate(request);
        cancellationToken.ThrowIfCancellationRequested();

        var requestHash = AppointmentMapping.Hash(request);
        if (await ReplayAsync(idempotencyKey, requestHash, cancellationToken) is { } original)
        {
            return original;
        }

        var customer = await customers.GetByIdAsync(request.CustomerId, cancellationToken)
                       ?? throw new EntityNotFoundException("Customer", request.CustomerId);
        var vehicle = await vehicles.GetByIdAsync(request.VehicleId, cancellationToken)
                      ?? throw new EntityNotFoundException("Vehicle", request.VehicleId);
        var dealership = await dealerships.GetByIdAsync(request.DealershipId, cancellationToken)
                         ?? throw new EntityNotFoundException("Dealership", request.DealershipId);
        var serviceType = await serviceTypes.GetByIdAsync(request.ServiceTypeId, cancellationToken)
                          ?? throw new EntityNotFoundException("Service type", request.ServiceTypeId);

        var startAt = request.StartAt.ToUniversalTime();
        var endAt = startAt + serviceType.Duration;
        if (!dealership.IsOpenDuring(startAt, endAt))
        {
            throw new DomainValidationException("The requested time is outside the dealership's working hours.");
        }

        var pairs = await selector.FindPairsAsync(dealership, serviceType, startAt, endAt, cancellationToken);

        // The exclusion constraints are the final re-check: a pair that was free when queried may be taken
        // by the time we commit. Each pair is tried once; the first commit that succeeds wins.
        foreach (var (technician, bay) in pairs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var appointment = Appointment.Book(
                Guid.NewGuid(), customer, vehicle, dealership, serviceType, technician, bay, startAt, clock.UtcNow);
            var response = appointment.ToDto();

            await appointments.AddAsync(appointment, cancellationToken);
            await idempotency.AddAsync(NewRecord(idempotencyKey, requestHash, response), cancellationToken);

            try
            {
                // Not abortable: once the commit starts, a client disconnect must not leave its outcome in doubt.
                await unitOfWork.SaveChangesAsync(CancellationToken.None);
                return response;
            }
            catch (ResourceUnavailableException)
            {
                // Lost the race for this pair (nothing was persisted); try the next one.
            }
            catch (DuplicateIdempotencyKeyException)
            {
                // The same request committed concurrently; return its result instead of a second appointment.
                return await ReplayAsync(idempotencyKey, requestHash, cancellationToken) ?? throw new DuplicateIdempotencyKeyException();
            }
        }

        // Every pair looked taken. If that is because an identical request (same key) committed meanwhile, that is not a conflict.
        return await ReplayAsync(idempotencyKey, requestHash, cancellationToken)
               ?? throw new ResourceUnavailableException("No technician and service bay are available for the requested time.");
    }

    public async Task<AppointmentDto> CancelAsync(string idempotencyKey, Guid appointmentId, CancellationToken cancellationToken = default)
    {
        RequestValidator.ValidateIdempotencyKey(idempotencyKey);
        cancellationToken.ThrowIfCancellationRequested();

        var requestHash = AppointmentMapping.HashCancel(appointmentId);
        if (await ReplayAsync(idempotencyKey, requestHash, cancellationToken) is { } original)
        {
            return original;
        }

        var appointment = await appointments.GetByIdAsync(appointmentId, cancellationToken)
                          ?? throw new EntityNotFoundException("Appointment", appointmentId);

        cancellationToken.ThrowIfCancellationRequested();
        appointment.Cancel(clock.UtcNow);
        var response = appointment.ToDto();
        await idempotency.AddAsync(NewRecord(idempotencyKey, requestHash, response), cancellationToken);

        try
        {
            await unitOfWork.SaveChangesAsync(CancellationToken.None);
            return response;
        }
        catch (DuplicateIdempotencyKeyException)
        {
            return await ReplayAsync(idempotencyKey, requestHash, cancellationToken) ?? throw new DuplicateIdempotencyKeyException();
        }
    }

    public async Task<AppointmentDto> GetAsync(Guid appointmentId, CancellationToken cancellationToken = default)
    {
        var appointment = await appointments.GetByIdAsync(appointmentId, cancellationToken)
                          ?? throw new EntityNotFoundException("Appointment", appointmentId);
        return appointment.ToDto();
    }

    /// <summary>The stored response if this key was already used with the same request; a conflict if with a different one.</summary>
    private async Task<AppointmentDto?> ReplayAsync(string key, string requestHash, CancellationToken cancellationToken)
    {
        var record = await idempotency.FindAsync(key, cancellationToken);
        if (record is null)
        {
            return null;
        }

        return record.RequestHash == requestHash
            ? JsonSerializer.Deserialize<AppointmentDto>(record.ResponseJson)
            : throw new IdempotencyConflictException();
    }

    private IdempotencyRecord NewRecord(string key, string requestHash, AppointmentDto response) =>
        new(key, requestHash, JsonSerializer.Serialize(response), clock.UtcNow);
}
