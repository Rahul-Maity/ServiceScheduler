namespace ServiceScheduler.Api.Contracts;

/// <summary>The HTTP request itself is malformed (maps to 400), as opposed to a business-rule violation (422).</summary>
public sealed class BadRequestException(string message) : Exception(message);
