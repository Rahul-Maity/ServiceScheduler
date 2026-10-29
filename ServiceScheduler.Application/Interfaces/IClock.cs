namespace ServiceScheduler.Application.Interfaces;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
