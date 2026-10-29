using System.Globalization;
using System.Text.RegularExpressions;

namespace ServiceScheduler.Api.Contracts;

/// <summary>
/// Parses ISO 8601 timestamps and insists on an explicit UTC offset (<c>Z</c> or <c>±hh:mm</c>). A bound
/// <see cref="DateTimeOffset"/> cannot tell "no offset" from "server-local", so the raw string is checked here.
/// </summary>
internal static partial class OffsetTimestamp
{
    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(:\d{2}(\.\d{1,7})?)?(Z|[+-]\d{2}:\d{2})$", RegexOptions.CultureInvariant)]
    private static partial Regex Format();

    public static DateTimeOffset Parse(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BadRequestException($"{field} is required.");
        }

        if (!Format().IsMatch(value)
            || !DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            throw new BadRequestException(
                $"{field} must be an ISO 8601 timestamp with a UTC offset, e.g. 2030-01-07T10:00:00Z or 2030-01-07T15:30:00+05:30.");
        }

        // PostgreSQL stores microseconds; .NET's "O" format sends 7 fractional digits. Truncate here so what we
        // echo back is exactly what is stored.
        return parsed.AddTicks(-(parsed.Ticks % (TimeSpan.TicksPerMillisecond / 1000)));
    }
}
