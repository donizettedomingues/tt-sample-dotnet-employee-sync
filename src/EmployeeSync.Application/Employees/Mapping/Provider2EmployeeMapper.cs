using System.Text.Json;
using EmployeeSync.Domain.Employees;

namespace EmployeeSync.Application.Employees.Mapping;

/// <summary>
/// Maps the nested provider2 payload (schemas/provider2.json):
/// employee_number, personal_info { given_name, family_name, email, mobile },
/// work_info { role, division, start_date, current_status }.
/// </summary>
/// <remarks>
/// The provider2 schema only requires the top-level objects, but TrackTik requires
/// first name, last name and email, so we require them here too and reject early with a 400.
/// </remarks>
public sealed class Provider2EmployeeMapper : IProviderEmployeeMapper
{
    // provider2 uses its own vocabulary; "on_leave" is still employed but not working -> inactive.
    private static readonly IReadOnlyDictionary<string, EmploymentStatus> StatusMap =
        new Dictionary<string, EmploymentStatus>(StringComparer.OrdinalIgnoreCase)
        {
            ["employed"] = EmploymentStatus.Active,
            ["on_leave"] = EmploymentStatus.Inactive,
            ["terminated"] = EmploymentStatus.Terminated,
        };

    public Provider Provider => Provider.Provider2;

    public MappingResult Map(JsonElement payload)
    {
        var reader = new PayloadReader();
        if (!reader.RequireObject(payload, "$"))
        {
            return MappingResult.Failure(reader.Errors);
        }

        var id = reader.ReadString(payload, "employee_number", "employee_number", required: true, FieldLimits.ProviderEmployeeId);
        var personal = reader.ReadObject(payload, "personal_info", "personal_info", required: true);
        var work = reader.ReadObject(payload, "work_info", "work_info", required: true);

        var firstName = reader.ReadString(personal, "given_name", "personal_info.given_name", required: personal is not null, FieldLimits.Name);
        var lastName = reader.ReadString(personal, "family_name", "personal_info.family_name", required: personal is not null, FieldLimits.Name);
        var email = reader.ReadEmail(personal, "email", "personal_info.email", required: personal is not null);
        var phone = reader.ReadString(personal, "mobile", "personal_info.mobile", required: false, FieldLimits.Phone);

        var position = reader.ReadString(work, "role", "work_info.role", required: false, FieldLimits.Position);
        var department = reader.ReadString(work, "division", "work_info.division", required: false, FieldLimits.Department);
        var startDate = reader.ReadDate(work, "start_date", "work_info.start_date");
        var status = reader.ReadEnum(work, "current_status", "work_info.current_status", StatusMap);

        if (reader.HasErrors)
        {
            return MappingResult.Failure(reader.Errors);
        }

        return MappingResult.Success(
            id!,
            new EmployeeDetails(firstName!, lastName!, email!, phone, position, department, startDate,
                status ?? EmploymentStatus.Active));
    }
}
