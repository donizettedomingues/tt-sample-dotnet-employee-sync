using EmployeeSync.Domain.Common;

namespace EmployeeSync.Domain.Employees;

/// <summary>Raised once per successful forward of an employee to TrackTik.</summary>
public sealed record EmployeeSynced(
    Guid EmployeeId,
    string Provider,
    string ProviderEmployeeId,
    string TrackTikEmployeeId,
    int Attempts,
    DateTimeOffset OccurredAt) : IDomainEvent;
