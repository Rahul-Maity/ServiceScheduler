using ServiceScheduler.Domain.Exceptions;

namespace ServiceScheduler.Domain.Entities;

internal static class Guard
{
    public static Guid NotEmpty(Guid value, string name) =>
        value == Guid.Empty ? throw new DomainValidationException($"{name} is required.") : value;

    public static string NotBlank(string? value, string name) =>
        string.IsNullOrWhiteSpace(value) ? throw new DomainValidationException($"{name} is required.") : value.Trim();
}
