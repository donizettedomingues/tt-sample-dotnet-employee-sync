using System.Text.Json;
using EmployeeSync.Application.Abstractions.TrackTik;
using EmployeeSync.Application.Employees.Mapping;
using EmployeeSync.Domain.Employees;

namespace EmployeeSync.UnitTests.Mapping;

/// <summary>The four sample payloads must produce sample-data/expected-tracktik-employees.json.</summary>
public class TrackTikEmployeeMapperTests
{
    public static TheoryData<string, string, TrackTikEmployee> Samples => new()
    {
        {
            "provider1",
            """{"emp_id":"P1_001","first_name":"Alice","last_name":"Johnson","email_address":"alice.johnson@provider1.com","phone":"+1-555-0101","job_title":"Security Officer","dept":"Security Operations","hire_date":"2024-01-15","employment_status":"active"}""",
            new TrackTikEmployee("provider1-P1_001", "Alice", "Johnson", "alice.johnson@provider1.com", "+1-555-0101", "Security Officer", "Security Operations", "2024-01-15", "active")
        },
        {
            "provider1",
            """{"emp_id":"P1_002","first_name":"Bob","last_name":"Smith","email_address":"bob.smith@provider1.com","phone":"+1-555-0102","job_title":"Supervisor","dept":"Security Operations","hire_date":"2023-11-20","employment_status":"active"}""",
            new TrackTikEmployee("provider1-P1_002", "Bob", "Smith", "bob.smith@provider1.com", "+1-555-0102", "Supervisor", "Security Operations", "2023-11-20", "active")
        },
        {
            "provider2",
            """{"employee_number":"P2_001","personal_info":{"given_name":"Carol","family_name":"Davis","email":"carol.davis@provider2.com","mobile":"+1-555-0201"},"work_info":{"role":"Security Guard","division":"Night Shift Security","start_date":"2024-02-01","current_status":"employed"}}""",
            new TrackTikEmployee("provider2-P2_001", "Carol", "Davis", "carol.davis@provider2.com", "+1-555-0201", "Security Guard", "Night Shift Security", "2024-02-01", "active")
        },
        {
            "provider2",
            """{"employee_number":"P2_002","personal_info":{"given_name":"David","family_name":"Wilson","email":"david.wilson@provider2.com","mobile":"+1-555-0202"},"work_info":{"role":"Team Lead","division":"Day Shift Security","start_date":"2023-12-15","current_status":"employed"}}""",
            new TrackTikEmployee("provider2-P2_002", "David", "Wilson", "david.wilson@provider2.com", "+1-555-0202", "Team Lead", "Day Shift Security", "2023-12-15", "active")
        },
    };

    [Theory]
    [MemberData(nameof(Samples))]
    public void Sample_payloads_map_to_expected_tracktik_employee(string provider, string payload, TrackTikEmployee expected)
    {
        Assert.True(ProviderNames.TryParse(provider, out var p));
        var resolver = new ProviderMapperResolver([new Provider1EmployeeMapper(), new Provider2EmployeeMapper()]);
        var mapped = resolver.Resolve(p).Map(JsonDocument.Parse(payload).RootElement);

        var employee = Employee.Create(p, mapped.ProviderEmployeeId!, mapped.Details!, DateTimeOffset.UnixEpoch);

        Assert.Equal(expected, TrackTikEmployeeMapper.ToTrackTik(employee));
    }

    [Fact]
    public void Same_raw_id_from_two_providers_gives_two_tracktik_ids()
    {
        Assert.NotEqual(
            Employee.DeriveTrackTikEmployeeId(Provider.Provider1, "001"),
            Employee.DeriveTrackTikEmployeeId(Provider.Provider2, "001"));
    }
}
