namespace EmployeeSync.Application.Abstractions.TrackTik;

/// <summary>Employee payload in the TrackTik schema (schemas/tracktik.json).</summary>
public sealed record TrackTikEmployee(
    string EmployeeId,
    string FirstName,
    string LastName,
    string Email,
    string? PhoneNumber,
    string? Position,
    string? Department,
    string? StartDate,
    string Status);

/// <summary>What we keep from a successful TrackTik response.</summary>
public sealed record TrackTikEmployeeResponse(string? Id, string EmployeeId);
