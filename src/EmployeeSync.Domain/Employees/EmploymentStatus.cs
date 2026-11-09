namespace EmployeeSync.Domain.Employees;

/// <summary>Employment status in TrackTik's vocabulary (active / inactive / terminated).</summary>
public enum EmploymentStatus
{
    Active = 1,
    Inactive = 2,
    Terminated = 3,
}

public static class EmploymentStatusExtensions
{
    public static string ToTrackTikValue(this EmploymentStatus status) => status switch
    {
        EmploymentStatus.Active => "active",
        EmploymentStatus.Inactive => "inactive",
        EmploymentStatus.Terminated => "terminated",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown employment status"),
    };
}
