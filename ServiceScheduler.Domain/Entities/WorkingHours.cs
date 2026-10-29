using ServiceScheduler.Domain.Exceptions;

namespace ServiceScheduler.Domain.Entities;

/// <summary>Opening window for one weekday, in the dealership's local time. Must not cross midnight.</summary>
public sealed class WorkingHours
{
    private WorkingHours()
    {
    }

    public WorkingHours(Guid dealershipId, DayOfWeek dayOfWeek, TimeOnly opensAt, TimeOnly closesAt)
    {
        if (opensAt >= closesAt)
        {
            throw new DomainValidationException("Working hours must open before they close.");
        }

        DealershipId = Guard.NotEmpty(dealershipId, nameof(dealershipId));
        DayOfWeek = dayOfWeek;
        OpensAt = opensAt;
        ClosesAt = closesAt;
    }

    public Guid DealershipId { get; private set; }
    public DayOfWeek DayOfWeek { get; private set; }
    public TimeOnly OpensAt { get; private set; }
    public TimeOnly ClosesAt { get; private set; }
}
