using EmployeeSync.Domain.Employees;

namespace EmployeeSync.Application.Employees.Mapping;

/// <summary>Outcome of validating + mapping one provider payload.</summary>
public sealed class MappingResult
{
    private MappingResult(string? providerEmployeeId, EmployeeDetails? details, IReadOnlyDictionary<string, string[]> errors)
    {
        ProviderEmployeeId = providerEmployeeId;
        Details = details;
        Errors = errors;
    }

    public string? ProviderEmployeeId { get; }
    public EmployeeDetails? Details { get; }
    public IReadOnlyDictionary<string, string[]> Errors { get; }
    public bool IsValid => Errors.Count == 0;

    public static MappingResult Success(string providerEmployeeId, EmployeeDetails details) =>
        new(providerEmployeeId, details, new Dictionary<string, string[]>());

    public static MappingResult Failure(IReadOnlyDictionary<string, string[]> errors) => new(null, null, errors);
}
