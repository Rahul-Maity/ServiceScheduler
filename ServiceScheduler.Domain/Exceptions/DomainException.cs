namespace ServiceScheduler.Domain.Exceptions;

public abstract class DomainException : Exception
{
    protected DomainException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

/// <summary>A request or entity state violates a business rule (bad input, unqualified resource, outside working hours).</summary>
public sealed class DomainValidationException : DomainException
{
    public DomainValidationException(string message) : base(message)
    {
    }
}

/// <summary>No suitable resource is free for the requested interval (maps to 409 Conflict).</summary>
public sealed class ResourceUnavailableException : DomainException
{
    public ResourceUnavailableException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

/// <summary>An appointment status change that is not allowed (e.g. cancelling twice).</summary>
public sealed class InvalidStateTransitionException : DomainException
{
    public InvalidStateTransitionException(string from, string to)
        : base($"Cannot change appointment status from {from} to {to}.")
    {
    }
}

/// <summary>A referenced entity does not exist (maps to 404).</summary>
public sealed class EntityNotFoundException : DomainException
{
    public EntityNotFoundException(string entityName, Guid id)
        : base($"{entityName} '{id}' was not found.")
    {
    }
}

/// <summary>An Idempotency-Key was reused for a different request.</summary>
public sealed class IdempotencyConflictException : DomainException
{
    public IdempotencyConflictException()
        : base("This Idempotency-Key was already used with a different request.")
    {
    }
}

/// <summary>Raised by Infrastructure when an idempotency record with the same key was committed concurrently.</summary>
public sealed class DuplicateIdempotencyKeyException : DomainException
{
    public DuplicateIdempotencyKeyException()
        : base("A request with this Idempotency-Key was already recorded.")
    {
    }
}
