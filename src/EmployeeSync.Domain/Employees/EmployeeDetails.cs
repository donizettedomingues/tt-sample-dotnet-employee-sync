namespace EmployeeSync.Domain.Employees;

/// <summary>
/// Provider-agnostic employee data, already normalised to TrackTik's vocabulary.
/// Produced by the provider mappers; consumed by the <see cref="Employee"/> aggregate.
/// </summary>
public sealed record EmployeeDetails(
    string FirstName,
    string LastName,
    string Email,
    string? PhoneNumber,
    string? Position,
    string? Department,
    DateOnly? StartDate,
    EmploymentStatus Status);
