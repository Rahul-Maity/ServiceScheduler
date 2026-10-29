namespace ServiceScheduler.Domain.Entities;

/// <summary>A qualification a technician can hold (e.g. "Brakes", "EV High Voltage").</summary>
public sealed class Skill
{
    private Skill()
    {
        Name = string.Empty;
    }

    public Skill(Guid id, string name)
    {
        Id = Guard.NotEmpty(id, nameof(id));
        Name = Guard.NotBlank(name, nameof(name));
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; }
}
