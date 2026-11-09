using EmployeeSync.Domain.Common;

namespace EmployeeSync.Domain.Employees;

/// <summary>
/// An employee received from a provider, plus the state of its sync to TrackTik.
/// Identity is (Provider, ProviderEmployeeId); records from different providers are never merged.
/// </summary>
public sealed class Employee : AggregateRoot
{
    public const int MaxErrorLength = 2000;

    // Required by EF Core.
    private Employee()
    {
        ProviderEmployeeId = null!;
        TrackTikEmployeeId = null!;
        FirstName = null!;
        LastName = null!;
        Email = null!;
    }

    private Employee(Guid id, Provider provider, string providerEmployeeId, DateTimeOffset now)
    {
        Id = id;
        Provider = provider;
        ProviderEmployeeId = providerEmployeeId;
        TrackTikEmployeeId = DeriveTrackTikEmployeeId(provider, providerEmployeeId);
        FirstName = null!;
        LastName = null!;
        Email = null!;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }
    public Provider Provider { get; private set; }
    public string ProviderEmployeeId { get; private set; }

    /// <summary>The <c>employeeId</c> sent to TrackTik, derived deterministically from the identity.</summary>
    public string TrackTikEmployeeId { get; private set; }

    public string FirstName { get; private set; }
    public string LastName { get; private set; }
    public string Email { get; private set; }
    public string? PhoneNumber { get; private set; }
    public string? Position { get; private set; }
    public string? Department { get; private set; }
    public DateOnly? StartDate { get; private set; }
    public EmploymentStatus Status { get; private set; }

    public SyncStatus SyncStatus { get; private set; }
    public string? LastError { get; private set; }
    public int Attempts { get; private set; }
    public DateTimeOffset? LastAttemptAt { get; private set; }
    public DateTimeOffset? LastSyncedAt { get; private set; }

    /// <summary>True once TrackTik has confirmed a create; later syncs use PUT instead of POST.</summary>
    public bool ExistsInTrackTik { get; private set; }

    /// <summary>The internal id TrackTik returned on create (informational).</summary>
    public string? TrackTikRecordId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static Employee Create(Provider provider, string providerEmployeeId, EmployeeDetails details, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(providerEmployeeId))
        {
            throw new ArgumentException("Provider employee id is required.", nameof(providerEmployeeId));
        }

        var employee = new Employee(Guid.NewGuid(), provider, providerEmployeeId.Trim(), now);
        employee.ApplyDetails(details, now);
        return employee;
    }

    /// <summary>
    /// Deterministic TrackTik employeeId: provider prefix + provider id, e.g. <c>provider1-P1_001</c>.
    /// The prefix keeps ids from different providers apart even if they share a raw id.
    /// </summary>
    public static string DeriveTrackTikEmployeeId(Provider provider, string providerEmployeeId) =>
        $"{provider.ToRouteValue()}-{providerEmployeeId.Trim()}";

    /// <summary>Replace the employee data with the latest submission and reset the sync state to pending.</summary>
    public void ApplyDetails(EmployeeDetails details, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(details);

        FirstName = details.FirstName;
        LastName = details.LastName;
        Email = details.Email;
        PhoneNumber = details.PhoneNumber;
        Position = details.Position;
        Department = details.Department;
        StartDate = details.StartDate;
        Status = details.Status;

        SyncStatus = SyncStatus.Pending;
        UpdatedAt = now;
    }

    /// <summary>Record that a forward attempt is about to start.</summary>
    public void RegisterAttempt(DateTimeOffset now)
    {
        Attempts++;
        LastAttemptAt = now;
        UpdatedAt = now;
    }

    public void MarkSynced(string? trackTikRecordId, DateTimeOffset now)
    {
        SyncStatus = SyncStatus.Synced;
        LastError = null;
        ExistsInTrackTik = true;
        TrackTikRecordId = trackTikRecordId ?? TrackTikRecordId;
        LastSyncedAt = now;
        UpdatedAt = now;

        Raise(new EmployeeSynced(Id, Provider.ToRouteValue(), ProviderEmployeeId, TrackTikEmployeeId, Attempts, now));
    }

    public void MarkFailed(string error, DateTimeOffset now)
    {
        SyncStatus = SyncStatus.Failed;
        LastError = string.IsNullOrWhiteSpace(error)
            ? "Unknown error"
            : error.Length > MaxErrorLength ? error[..MaxErrorLength] : error;
        UpdatedAt = now;
    }

    /// <summary>TrackTik said the employee does not exist (e.g. deleted there); the next forward must POST.</summary>
    public void MarkMissingInTrackTik() => ExistsInTrackTik = false;
}
