namespace ServiceScheduler.Domain.Entities;

/// <summary>Equipment a service bay can offer (e.g. "Lift", "Alignment Rig").</summary>
public sealed class Capability
{
    private Capability()
    {
        Name = string.Empty;
    }

    public Capability(Guid id, string name)
    {
        Id = Guard.NotEmpty(id, nameof(id));
        Name = Guard.NotBlank(name, nameof(name));
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; }
}
