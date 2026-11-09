namespace EmployeeSync.Application.Abstractions.TrackTik;

/// <summary>Port to the TrackTik Employees API. Implementations handle authentication.</summary>
public interface ITrackTikClient
{
    /// <summary>POST /employees.</summary>
    /// <exception cref="TrackTikApiException">TrackTik could not be reached or rejected the request.</exception>
    Task<TrackTikEmployeeResponse> CreateEmployeeAsync(TrackTikEmployee employee, CancellationToken cancellationToken);

    /// <summary>PUT /employees/{employeeId}. Returns <c>null</c> when TrackTik answers 404.</summary>
    /// <exception cref="TrackTikApiException">TrackTik could not be reached or rejected the request.</exception>
    Task<TrackTikEmployeeResponse?> UpdateEmployeeAsync(string employeeId, TrackTikEmployee employee, CancellationToken cancellationToken);
}
