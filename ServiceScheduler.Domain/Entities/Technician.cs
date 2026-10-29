namespace ServiceScheduler.Domain.Entities;

public sealed class Technician
{
    private readonly List<Skill> _skills = new();

    private Technician()
    {
        Name = string.Empty;
    }

    public Technician(Guid id, Guid dealershipId, string name, IEnumerable<Skill> skills)
    {
        Id = Guard.NotEmpty(id, nameof(id));
        DealershipId = Guard.NotEmpty(dealershipId, nameof(dealershipId));
        Name = Guard.NotBlank(name, nameof(name));
        _skills.AddRange(skills.DistinctBy(s => s.Id));
    }

    public Guid Id { get; private set; }
    public Guid DealershipId { get; private set; }
    public string Name { get; private set; }
    public IReadOnlyCollection<Skill> Skills => _skills;

    /// <summary>True when the technician holds every skill the service type requires.</summary>
    public bool IsQualifiedFor(ServiceType serviceType) =>
        serviceType.RequiredSkills.All(required => _skills.Any(s => s.Id == required.Id));
}
