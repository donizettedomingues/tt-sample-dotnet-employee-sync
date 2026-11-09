namespace EmployeeSync.Domain.Employees;

/// <summary>State of the forward to TrackTik. The contract allows exactly these three values.</summary>
public enum SyncStatus
{
    Pending = 1,
    Synced = 2,
    Failed = 3,
}

public static class SyncStatusExtensions
{
    public static string ToApiValue(this SyncStatus status) => status switch
    {
        SyncStatus.Pending => "pending",
        SyncStatus.Synced => "synced",
        SyncStatus.Failed => "failed",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown sync status"),
    };
}
