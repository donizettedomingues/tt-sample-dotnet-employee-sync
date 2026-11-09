using EmployeeSync.Application.Abstractions.Persistence;
using EmployeeSync.Domain.Employees;

namespace EmployeeSync.UnitTests.Fakes;

/// <summary>Repository + unit of work fake that records the sync status at every commit.</summary>
public sealed class InMemoryEmployeeStore : IEmployeeRepository, IUnitOfWork
{
    private readonly List<Employee> _pending = [];

    public List<Employee> Committed { get; } = [];

    public List<SyncStatus> CommittedStatuses { get; } = [];

    public Task<Employee?> FindAsync(Provider provider, string providerEmployeeId, CancellationToken cancellationToken) =>
        Task.FromResult(Committed.SingleOrDefault(e => e.Provider == provider && e.ProviderEmployeeId == providerEmployeeId));

    public void Add(Employee employee) => _pending.Add(employee);

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        Committed.AddRange(_pending);
        _pending.Clear();
        CommittedStatuses.AddRange(Committed.Select(e => e.SyncStatus));
        return Task.CompletedTask;
    }
}
