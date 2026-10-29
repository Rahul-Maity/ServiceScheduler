using ServiceScheduler.Application.Interfaces;
using ServiceScheduler.Domain.Entities;
using ServiceScheduler.Domain.Interfaces;

namespace ServiceScheduler.Application.Tests;

/// <summary>
/// In-memory stand-in for every Domain/Application persistence interface. Mimics the Unit of Work contract:
/// adds are staged and only become visible on SaveChangesAsync; a failed save discards what was staged.
/// </summary>
public sealed class FakeStore :
    ICustomerRepository, IVehicleRepository, IDealershipRepository, IServiceTypeRepository,
    IAppointmentRepository, ITechnicianRepository, IServiceBayRepository, IIdempotencyStore, IUnitOfWork
{
    private readonly List<Appointment> _stagedAppointments = [];
    private readonly List<IdempotencyRecord> _stagedRecords = [];
    private readonly Queue<(Exception Error, Action? SideEffect)> _saveFailures = new();

    public Dictionary<Guid, Customer> Customers { get; } = [];
    public Dictionary<Guid, Vehicle> Vehicles { get; } = [];
    public Dictionary<Guid, Dealership> Dealerships { get; } = [];
    public Dictionary<Guid, ServiceType> ServiceTypes { get; } = [];
    public List<Technician> Technicians { get; } = [];
    public List<ServiceBay> Bays { get; } = [];

    /// <summary>Committed state.</summary>
    public List<Appointment> Appointments { get; } = [];
    public Dictionary<string, IdempotencyRecord> Records { get; } = [];

    public int SaveCalls { get; private set; }
    public int ResourceQueries { get; private set; }
    public int StagedCount => _stagedAppointments.Count + _stagedRecords.Count;
    public List<CancellationToken> SaveTokens { get; } = [];

    /// <summary>For each save: the (technician, bay) of the staged appointment, to check pairs are not retried.</summary>
    public List<(Guid TechnicianId, Guid BayId)> SaveAttempts { get; } = [];
    public (int Appointments, int Records) LastSaveStaged { get; private set; }

    /// <summary>Runs whenever candidates are queried (lets a test cancel a token mid-flow).</summary>
    public Action? OnResourceQuery { get; set; }

    /// <summary>The next save throws <paramref name="error"/> after running <paramref name="sideEffect"/> (a racing request).</summary>
    public void FailNextSave(Exception error, Action? sideEffect = null) => _saveFailures.Enqueue((error, sideEffect));

    Task<Customer?> ICustomerRepository.GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Customers.GetValueOrDefault(id));

    Task<Vehicle?> IVehicleRepository.GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Vehicles.GetValueOrDefault(id));

    Task<Dealership?> IDealershipRepository.GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Dealerships.GetValueOrDefault(id));

    Task<ServiceType?> IServiceTypeRepository.GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(ServiceTypes.GetValueOrDefault(id));

    Task<Appointment?> IAppointmentRepository.GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Appointments.FirstOrDefault(a => a.Id == id));

    public Task AddAsync(Appointment appointment, CancellationToken cancellationToken = default)
    {
        _stagedAppointments.Add(appointment);
        return Task.CompletedTask;
    }

    public Task AddAsync(IdempotencyRecord record, CancellationToken cancellationToken = default)
    {
        _stagedRecords.Add(record);
        return Task.CompletedTask;
    }

    public Task<IdempotencyRecord?> FindAsync(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult(Records.GetValueOrDefault(key));

    Task<IReadOnlyList<Technician>> ITechnicianRepository.FindAvailableAsync(
        Guid dealershipId, ServiceType serviceType, DateTimeOffset startAt, DateTimeOffset endAt, CancellationToken cancellationToken)
    {
        ResourceQueries++;
        OnResourceQuery?.Invoke();
        IReadOnlyList<Technician> result = Technicians
            .Where(t => t.DealershipId == dealershipId && t.IsQualifiedFor(serviceType)
                        && !Appointments.Any(a => a.TechnicianId == t.Id && a.ReservesResources && a.Overlaps(startAt, endAt)))
            .OrderBy(t => t.Name).ToList();
        return Task.FromResult(result);
    }

    Task<IReadOnlyList<ServiceBay>> IServiceBayRepository.FindAvailableAsync(
        Guid dealershipId, ServiceType serviceType, DateTimeOffset startAt, DateTimeOffset endAt, CancellationToken cancellationToken)
    {
        IReadOnlyList<ServiceBay> result = Bays
            .Where(b => b.DealershipId == dealershipId && b.IsCompatibleWith(serviceType)
                        && !Appointments.Any(a => a.ServiceBayId == b.Id && a.ReservesResources && a.Overlaps(startAt, endAt)))
            .OrderBy(b => b.Name).ToList();
        return Task.FromResult(result);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCalls++;
        SaveTokens.Add(cancellationToken);
        LastSaveStaged = (_stagedAppointments.Count, _stagedRecords.Count);
        if (_stagedAppointments.Count == 1)
        {
            SaveAttempts.Add((_stagedAppointments[0].TechnicianId, _stagedAppointments[0].ServiceBayId));
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (_saveFailures.TryDequeue(out var failure))
        {
            _stagedAppointments.Clear();
            _stagedRecords.Clear();
            failure.SideEffect?.Invoke();
            throw failure.Error;
        }

        Appointments.AddRange(_stagedAppointments);
        foreach (var record in _stagedRecords)
        {
            Records[record.Key] = record;
        }

        _stagedAppointments.Clear();
        _stagedRecords.Clear();
        return Task.CompletedTask;
    }
}
