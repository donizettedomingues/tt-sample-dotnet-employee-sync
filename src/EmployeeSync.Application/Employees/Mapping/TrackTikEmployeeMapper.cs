using System.Globalization;
using EmployeeSync.Application.Abstractions.TrackTik;
using EmployeeSync.Domain.Employees;

namespace EmployeeSync.Application.Employees.Mapping;

/// <summary>Maps the stored aggregate to the TrackTik employee schema.</summary>
public static class TrackTikEmployeeMapper
{
    public static TrackTikEmployee ToTrackTik(Employee employee) => new(
        employee.TrackTikEmployeeId,
        employee.FirstName,
        employee.LastName,
        employee.Email,
        employee.PhoneNumber,
        employee.Position,
        employee.Department,
        employee.StartDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        employee.Status.ToTrackTikValue());
}
