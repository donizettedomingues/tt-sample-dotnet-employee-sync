using System.Text.Json;
using EmployeeSync.Domain.Employees;

namespace EmployeeSync.Application.Employees.Mapping;

/// <summary>
/// Strategy: one implementation per provider. Validates the provider-specific JSON and maps it
/// to the provider-agnostic <see cref="EmployeeDetails"/>.
/// </summary>
public interface IProviderEmployeeMapper
{
    Provider Provider { get; }

    MappingResult Map(JsonElement payload);
}
