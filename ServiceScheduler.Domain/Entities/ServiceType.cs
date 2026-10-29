using ServiceScheduler.Domain.Exceptions;

namespace ServiceScheduler.Domain.Entities;

public sealed class ServiceType
{
    private readonly List<Skill> _requiredSkills = new();
    private readonly List<Capability> _requiredCapabilities = new();

    private ServiceType()
    {
        Name = string.Empty;
    }

    public ServiceType(
        Guid id,
        string name,
        TimeSpan duration,
        IEnumerable<Skill> requiredSkills,
        IEnumerable<Capability> requiredCapabilities)
    {
        if (duration <= TimeSpan.Zero)
        {
            throw new DomainValidationException("Service duration must be positive.");
        }

        Id = Guard.NotEmpty(id, nameof(id));
        Name = Guard.NotBlank(name, nameof(name));
        Duration = duration;
        _requiredSkills.AddRange(requiredSkills.DistinctBy(s => s.Id));
        _requiredCapabilities.AddRange(requiredCapabilities.DistinctBy(c => c.Id));
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public TimeSpan Duration { get; private set; }
    public IReadOnlyCollection<Skill> RequiredSkills => _requiredSkills;
    public IReadOnlyCollection<Capability> RequiredCapabilities => _requiredCapabilities;
}
