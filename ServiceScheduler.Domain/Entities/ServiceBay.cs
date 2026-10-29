namespace ServiceScheduler.Domain.Entities;

public sealed class ServiceBay
{
    private readonly List<Capability> _capabilities = new();

    private ServiceBay()
    {
        Name = string.Empty;
    }

    public ServiceBay(Guid id, Guid dealershipId, string name, IEnumerable<Capability> capabilities)
    {
        Id = Guard.NotEmpty(id, nameof(id));
        DealershipId = Guard.NotEmpty(dealershipId, nameof(dealershipId));
        Name = Guard.NotBlank(name, nameof(name));
        _capabilities.AddRange(capabilities.DistinctBy(c => c.Id));
    }

    public Guid Id { get; private set; }
    public Guid DealershipId { get; private set; }
    public string Name { get; private set; }
    public IReadOnlyCollection<Capability> Capabilities => _capabilities;

    /// <summary>True when the bay offers every capability the service type requires.</summary>
    public bool IsCompatibleWith(ServiceType serviceType) =>
        serviceType.RequiredCapabilities.All(required => _capabilities.Any(c => c.Id == required.Id));
}
