using EmployeeSync.Application.Abstractions.TrackTik;
using EmployeeSync.Domain.Employees;

namespace EmployeeSync.UnitTests.Fakes;

public sealed class FakeTrackTikClient : ITrackTikClient
{
    private readonly InMemoryEmployeeStore? _store;

    public FakeTrackTikClient(InMemoryEmployeeStore? store = null) => _store = store;

    public List<(string Method, TrackTikEmployee Payload)> Calls { get; } = [];

    /// <summary>Status committed to the store at the moment TrackTik was called.</summary>
    public List<SyncStatus?> StatusSeenAtCall { get; } = [];

    public Exception? FailWith { get; set; }

    public bool UpdateReturnsNotFound { get; set; }

    public Task<TrackTikEmployeeResponse> CreateEmployeeAsync(TrackTikEmployee employee, CancellationToken cancellationToken)
    {
        Record("POST", employee);
        if (FailWith is not null)
        {
            throw FailWith;
        }

        return Task.FromResult(new TrackTikEmployeeResponse("tt-" + employee.EmployeeId, employee.EmployeeId));
    }

    public Task<TrackTikEmployeeResponse?> UpdateEmployeeAsync(string employeeId, TrackTikEmployee employee, CancellationToken cancellationToken)
    {
        Record("PUT", employee);
        if (FailWith is not null)
        {
            throw FailWith;
        }

        return Task.FromResult(UpdateReturnsNotFound ? null : new TrackTikEmployeeResponse(null, employeeId));
    }

    private void Record(string method, TrackTikEmployee employee)
    {
        Calls.Add((method, employee));
        StatusSeenAtCall.Add(_store?.CommittedStatuses.LastOrDefault());
    }
}
