using EmployeeSync.Domain.Employees;

namespace EmployeeSync.UnitTests.Employees;

public class EmployeeTests
{
    private static readonly EmployeeDetails Details =
        new("Alice", "Johnson", "alice@x.co", null, null, null, null, EmploymentStatus.Active);

    [Fact]
    public void New_employee_is_pending_with_no_attempts()
    {
        var e = Employee.Create(Provider.Provider2, "P2_001", Details, DateTimeOffset.UnixEpoch);

        Assert.Equal(SyncStatus.Pending, e.SyncStatus);
        Assert.Equal(0, e.Attempts);
        Assert.Equal("provider2-P2_001", e.TrackTikEmployeeId);
        Assert.False(e.ExistsInTrackTik);
    }

    [Fact]
    public void MarkFailed_truncates_long_errors()
    {
        var e = Employee.Create(Provider.Provider1, "1", Details, DateTimeOffset.UnixEpoch);

        e.MarkFailed(new string('x', Employee.MaxErrorLength + 50), DateTimeOffset.UnixEpoch);

        Assert.Equal(Employee.MaxErrorLength, e.LastError!.Length);
        Assert.Empty(e.DomainEvents);
    }

    [Fact]
    public void MarkSynced_clears_error_and_raises_event()
    {
        var e = Employee.Create(Provider.Provider1, "1", Details, DateTimeOffset.UnixEpoch);
        e.MarkFailed("x", DateTimeOffset.UnixEpoch);

        e.MarkSynced("abc", DateTimeOffset.UnixEpoch);

        Assert.Null(e.LastError);
        Assert.True(e.ExistsInTrackTik);
        Assert.IsType<EmployeeSynced>(Assert.Single(e.DomainEvents));
    }
}
