using ServiceScheduler.Application.DTOs;
using ServiceScheduler.Application.Validators;
using ServiceScheduler.Domain.Exceptions;

namespace ServiceScheduler.Application.Tests;

public class RequestValidatorTests
{
    private static readonly DateTimeOffset Start = World.At(10);

    [Fact]
    public void Valid_requests_pass()
    {
        var id = Guid.NewGuid();
        RequestValidator.Validate(new AvailabilityRequest(id, id, Start));
        RequestValidator.Validate(new BookAppointmentRequest(id, id, id, id, Start));
        RequestValidator.ValidateIdempotencyKey("abc-123");
    }

    [Fact]
    public void Availability_requires_dealership_service_type_and_start()
    {
        var id = Guid.NewGuid();
        Assert.Throws<DomainValidationException>(() => RequestValidator.Validate(new AvailabilityRequest(Guid.Empty, id, Start)));
        Assert.Throws<DomainValidationException>(() => RequestValidator.Validate(new AvailabilityRequest(id, Guid.Empty, Start)));
        Assert.Throws<DomainValidationException>(() => RequestValidator.Validate(new AvailabilityRequest(id, id, default)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Booking_requires_every_id(int emptyIndex)
    {
        var ids = Enumerable.Range(0, 4).Select(i => i == emptyIndex ? Guid.Empty : Guid.NewGuid()).ToArray();
        var request = new BookAppointmentRequest(ids[0], ids[1], ids[2], ids[3], Start);

        Assert.Throws<DomainValidationException>(() => RequestValidator.Validate(request));
    }

    [Fact]
    public void Booking_requires_a_start_time()
    {
        var id = Guid.NewGuid();
        Assert.Throws<DomainValidationException>(() =>
            RequestValidator.Validate(new BookAppointmentRequest(id, id, id, id, default)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Idempotency_key_is_required(string? key) =>
        Assert.Throws<DomainValidationException>(() => RequestValidator.ValidateIdempotencyKey(key));

    [Fact]
    public void Idempotency_key_longer_than_the_stored_column_is_rejected() =>
        Assert.Throws<DomainValidationException>(() => RequestValidator.ValidateIdempotencyKey(new string('k', 256)));
}
