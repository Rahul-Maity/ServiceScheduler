using ServiceScheduler.Application.Interfaces;

namespace ServiceScheduler.Infrastructure.Services;

internal sealed class SystemClock : IClock
{
    private const long TicksPerMicrosecond = TimeSpan.TicksPerMillisecond / 1000;

    /// <summary>Truncated to microseconds, the precision PostgreSQL stores, so a response never differs from what is read back.</summary>
    public DateTimeOffset UtcNow
    {
        get
        {
            var now = DateTimeOffset.UtcNow;
            return now.AddTicks(-(now.Ticks % TicksPerMicrosecond));
        }
    }
}
