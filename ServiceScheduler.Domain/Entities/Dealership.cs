using ServiceScheduler.Domain.Exceptions;

namespace ServiceScheduler.Domain.Entities;

public sealed class Dealership
{
    private readonly List<WorkingHours> _workingHours = new();

    private Dealership()
    {
        Name = string.Empty;
        TimeZoneId = string.Empty;
    }

    public Dealership(Guid id, string name, string timeZoneId, IEnumerable<WorkingHours>? workingHours = null)
    {
        Id = Guard.NotEmpty(id, nameof(id));
        Name = Guard.NotBlank(name, nameof(name));
        TimeZoneId = Guard.NotBlank(timeZoneId, nameof(timeZoneId));
        ResolveTimeZone();

        foreach (var hours in workingHours ?? [])
        {
            SetWorkingHours(hours.DayOfWeek, hours.OpensAt, hours.ClosesAt);
        }
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; }

    /// <summary>Time zone the working hours are expressed in.</summary>
    public string TimeZoneId { get; private set; }

    public IReadOnlyCollection<WorkingHours> WorkingHours => _workingHours;

    /// <summary>Sets (or replaces) the opening window for a weekday.</summary>
    public void SetWorkingHours(DayOfWeek dayOfWeek, TimeOnly opensAt, TimeOnly closesAt)
    {
        var hours = new WorkingHours(Id, dayOfWeek, opensAt, closesAt);
        _workingHours.RemoveAll(h => h.DayOfWeek == dayOfWeek);
        _workingHours.Add(hours);
    }

    /// <summary>
    /// True when the half-open interval [startAt, endAt) lies inside a single day's working hours.
    /// Ending exactly at closing time is allowed.
    /// </summary>
    public bool IsOpenDuring(DateTimeOffset startAt, DateTimeOffset endAt)
    {
        var zone = ResolveTimeZone();
        var localStart = TimeZoneInfo.ConvertTime(startAt, zone).DateTime;
        var localEnd = TimeZoneInfo.ConvertTime(endAt, zone).DateTime;

        var hours = _workingHours.FirstOrDefault(h => h.DayOfWeek == localStart.DayOfWeek);
        if (hours is null)
        {
            return false;
        }

        var opensAt = localStart.Date + hours.OpensAt.ToTimeSpan();
        var closesAt = localStart.Date + hours.ClosesAt.ToTimeSpan();
        return localStart >= opensAt && localEnd <= closesAt;
    }

    private TimeZoneInfo ResolveTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            throw new DomainValidationException($"Unknown time zone '{TimeZoneId}'.");
        }
    }
}
