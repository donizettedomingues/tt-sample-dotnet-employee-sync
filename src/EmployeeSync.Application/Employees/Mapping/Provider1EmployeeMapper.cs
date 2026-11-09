using System.Text.Json;
using EmployeeSync.Domain.Employees;

namespace EmployeeSync.Application.Employees.Mapping;

/// <summary>
/// Maps the flat provider1 payload (schemas/provider1.json):
/// emp_id, first_name, last_name, email_address, phone, job_title, dept, hire_date, employment_status.
/// </summary>
public sealed class Provider1EmployeeMapper : IProviderEmployeeMapper
{
    private static readonly IReadOnlyDictionary<string, EmploymentStatus> StatusMap =
        new Dictionary<string, EmploymentStatus>(StringComparer.OrdinalIgnoreCase)
        {
            ["active"] = EmploymentStatus.Active,
            ["inactive"] = EmploymentStatus.Inactive,
            ["terminated"] = EmploymentStatus.Terminated,
        };

    public Provider Provider => Provider.Provider1;

    public MappingResult Map(JsonElement payload)
    {
        var reader = new PayloadReader();
        if (!reader.RequireObject(payload, "$"))
        {
            return MappingResult.Failure(reader.Errors);
        }

        var id = reader.ReadString(payload, "emp_id", "emp_id", required: true, FieldLimits.ProviderEmployeeId);
        var firstName = reader.ReadString(payload, "first_name", "first_name", required: true, FieldLimits.Name);
        var lastName = reader.ReadString(payload, "last_name", "last_name", required: true, FieldLimits.Name);
        var email = reader.ReadEmail(payload, "email_address", "email_address", required: true);
        var phone = reader.ReadString(payload, "phone", "phone", required: false, FieldLimits.Phone);
        var position = reader.ReadString(payload, "job_title", "job_title", required: false, FieldLimits.Position);
        var department = reader.ReadString(payload, "dept", "dept", required: false, FieldLimits.Department);
        var hireDate = reader.ReadDate(payload, "hire_date", "hire_date");
        var status = reader.ReadEnum(payload, "employment_status", "employment_status", StatusMap);

        if (reader.HasErrors)
        {
            return MappingResult.Failure(reader.Errors);
        }

        return MappingResult.Success(
            id!,
            new EmployeeDetails(firstName!, lastName!, email!, phone, position, department, hireDate,
                status ?? EmploymentStatus.Active));
    }
}
