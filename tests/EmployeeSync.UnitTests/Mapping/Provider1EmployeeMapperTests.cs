using System.Text.Json;
using EmployeeSync.Application.Employees.Mapping;
using EmployeeSync.Domain.Employees;

namespace EmployeeSync.UnitTests.Mapping;

public class Provider1EmployeeMapperTests
{
    private readonly Provider1EmployeeMapper _mapper = new();

    private static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public void Maps_sample_payload_to_tracktik_fields()
    {
        var result = _mapper.Map(Json("""
            {
              "emp_id": "P1_001", "first_name": "Alice", "last_name": "Johnson",
              "email_address": "alice.johnson@provider1.com", "phone": "+1-555-0101",
              "job_title": "Security Officer", "dept": "Security Operations",
              "hire_date": "2024-01-15", "employment_status": "active"
            }
            """));

        Assert.True(result.IsValid);
        Assert.Equal("P1_001", result.ProviderEmployeeId);
        Assert.Equal(
            new EmployeeDetails("Alice", "Johnson", "alice.johnson@provider1.com", "+1-555-0101",
                "Security Officer", "Security Operations", new DateOnly(2024, 1, 15), EmploymentStatus.Active),
            result.Details);
    }

    [Theory]
    [InlineData("active", EmploymentStatus.Active)]
    [InlineData("inactive", EmploymentStatus.Inactive)]
    [InlineData("terminated", EmploymentStatus.Terminated)]
    [InlineData("TERMINATED", EmploymentStatus.Terminated)]
    public void Maps_employment_status(string value, EmploymentStatus expected)
    {
        var result = _mapper.Map(Json($$"""
            { "emp_id": "1", "first_name": "A", "last_name": "B", "email_address": "a@b.co", "employment_status": "{{value}}" }
            """));

        Assert.Equal(expected, result.Details!.Status);
    }

    [Fact]
    public void Defaults_optional_fields_and_status_to_active()
    {
        var result = _mapper.Map(Json("""{ "emp_id": " 7 ", "first_name": " A ", "last_name": "B", "email_address": "A@B.CO" }"""));

        Assert.True(result.IsValid);
        Assert.Equal("7", result.ProviderEmployeeId);
        Assert.Equal("A", result.Details!.FirstName);
        Assert.Equal("a@b.co", result.Details.Email);
        Assert.Null(result.Details.PhoneNumber);
        Assert.Null(result.Details.StartDate);
        Assert.Equal(EmploymentStatus.Active, result.Details.Status);
    }

    [Fact]
    public void Reports_every_invalid_field_with_the_provider_field_name()
    {
        var result = _mapper.Map(Json("""
            { "emp_id": 42, "first_name": "", "email_address": "not-an-email",
              "hire_date": "15/01/2024", "employment_status": "retired" }
            """));

        Assert.False(result.IsValid);
        Assert.Equal(
            ["email_address", "emp_id", "employment_status", "first_name", "hire_date", "last_name"],
            result.Errors.Keys.Order().ToArray());
        Assert.Contains("Must be a string.", result.Errors["emp_id"]);
    }

    [Fact]
    public void Rejects_non_object_payload()
    {
        var result = _mapper.Map(Json("[1,2]"));

        Assert.False(result.IsValid);
        Assert.Contains("$", result.Errors.Keys);
    }

    [Fact]
    public void Rejects_values_longer_than_the_column()
    {
        var longName = new string('x', FieldLimits.Name + 1);
        var result = _mapper.Map(Json($$"""{ "emp_id": "1", "first_name": "{{longName}}", "last_name": "B", "email_address": "a@b.co" }"""));

        Assert.Contains("first_name", result.Errors.Keys);
    }
}
