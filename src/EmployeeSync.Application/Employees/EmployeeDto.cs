using EmployeeSync.Domain.Employees;

namespace EmployeeSync.Application.Employees;

/// <summary>Stored employee plus its sync state, as returned by the API.</summary>
public sealed record EmployeeDto(
    Guid Id,
    string Provider,
    string ProviderEmployeeId,
    string TrackTikEmployeeId,
    string FirstName,
    string LastName,
    string Email,
    string? PhoneNumber,
    string? Position,
    string? Department,
    DateOnly? StartDate,
    string Status,
    string SyncStatus,
    string? LastError,
    int Attempts,
    DateTimeOffset? LastAttemptAt,
    DateTimeOffset? LastSyncedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static EmployeeDto From(Employee e) => new(
        e.Id,
        e.Provider.ToRouteValue(),
        e.ProviderEmployeeId,
        e.TrackTikEmployeeId,
        e.FirstName,
        e.LastName,
        e.Email,
        e.PhoneNumber,
        e.Position,
        e.Department,
        e.StartDate,
        e.Status.ToTrackTikValue(),
        e.SyncStatus.ToApiValue(),
        e.LastError,
        e.Attempts,
        e.LastAttemptAt,
        e.LastSyncedAt,
        e.CreatedAt,
        e.UpdatedAt);
}
