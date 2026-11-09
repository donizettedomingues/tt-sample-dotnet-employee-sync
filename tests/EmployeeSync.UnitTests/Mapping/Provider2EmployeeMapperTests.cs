using System.Text.Json;
using EmployeeSync.Application.Employees.Mapping;
using EmployeeSync.Domain.Employees;

namespace EmployeeSync.UnitTests.Mapping;

public class Provider2EmployeeMapperTests
{
    private readonly Provider2EmployeeMapper _mapper = new();

    private static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public void Maps_nested_sample_payload()
    {
        var result = _mapper.Map(Json("""
            {
              "employee_number": "P2_001",
              "personal_info": { "given_name": "Carol", "family_name": "Davis", "email": "carol.davis@provider2.com", "mobile": "+1-555-0201" },
              "work_info": { "role": "Security Guard", "division": "Night Shift Security", "start_date": "2024-02-01", "current_status": "employed" }
            }
            """));

        Assert.True(result.IsValid);
        Assert.Equal("P2_001", result.ProviderEmployeeId);
        Assert.Equal(
            new EmployeeDetails("Carol", "Davis", "carol.davis@provider2.com", "+1-555-0201",
                "Security Guard", "Night Shift Security", new DateOnly(2024, 2, 1), EmploymentStatus.Active),
            result.Details);
    }

    [Theory]
    [InlineData("employed", EmploymentStatus.Active)]
    [InlineData("on_leave", EmploymentStatus.Inactive)]
    [InlineData("terminated", EmploymentStatus.Terminated)]
    public void Translates_provider2_status_vocabulary(string value, EmploymentStatus expected)
    {
        var result = _mapper.Map(Json($$"""
            {
              "employee_number": "9",
              "personal_info": { "given_name": "A", "family_name": "B", "email": "a@b.co" },
              "work_info": { "current_status": "{{value}}" }
            }
            """));

        Assert.Equal(expected, result.Details!.Status);
    }

    [Fact]
    public void Rejects_provider1_vocabulary_for_status()
    {
        var result = _mapper.Map(Json("""
            { "employee_number": "9", "personal_info": { "given_name": "A", "family_name": "B", "email": "a@b.co" },
              "work_info": { "current_status": "active" } }
            """));

        Assert.Contains("work_info.current_status", result.Errors.Keys);
    }

    [Fact]
    public void Requires_the_nested_objects()
    {
        var result = _mapper.Map(Json("""{ "employee_number": "9", "personal_info": "Carol" }"""));

        Assert.False(result.IsValid);
        Assert.Contains("personal_info", result.Errors.Keys);
        Assert.Contains("work_info", result.Errors.Keys);
    }

    [Fact]
    public void Requires_the_fields_tracktik_needs_with_nested_paths()
    {
        var result = _mapper.Map(Json("""
            { "employee_number": "9", "personal_info": { "email": "bad" }, "work_info": { "start_date": "2024-13-01" } }
            """));

        Assert.Equal(
            ["personal_info.email", "personal_info.family_name", "personal_info.given_name", "work_info.start_date"],
            result.Errors.Keys.Order().ToArray());
    }
}
