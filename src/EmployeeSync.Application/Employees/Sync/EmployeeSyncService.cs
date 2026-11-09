using EmployeeSync.Application.Abstractions.Events;
using EmployeeSync.Application.Abstractions.Persistence;
using EmployeeSync.Application.Abstractions.TrackTik;
using EmployeeSync.Application.Employees.Mapping;
using EmployeeSync.Domain.Employees;
using Microsoft.Extensions.Logging;

namespace EmployeeSync.Application.Employees.Sync;

/// <summary>
/// Forwards one already-persisted employee to TrackTik and records the outcome.
/// Kept separate from the command handler so a background retry job could reuse it.
/// </summary>
public sealed partial class EmployeeSyncService
{
    private readonly ITrackTikClient _trackTik;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEventPublisher _eventPublisher;
    private readonly TimeProvider _time;
    private readonly ILogger<EmployeeSyncService> _logger;

    public EmployeeSyncService(
        ITrackTikClient trackTik,
        IUnitOfWork unitOfWork,
        IEventPublisher eventPublisher,
        TimeProvider time,
        ILogger<EmployeeSyncService> logger)
    {
        _trackTik = trackTik;
        _unitOfWork = unitOfWork;
        _eventPublisher = eventPublisher;
        _time = time;
        _logger = logger;
    }

    /// <summary>
    /// PUT when TrackTik already has the employee, otherwise POST. Leaves the employee
    /// <c>synced</c> or <c>failed</c>; never throws for upstream failures.
    /// </summary>
    public async Task SyncAsync(Employee employee, CancellationToken cancellationToken)
    {
        employee.RegisterAttempt(_time.GetUtcNow());
        var payload = TrackTikEmployeeMapper.ToTrackTik(employee);

        try
        {
            TrackTikEmployeeResponse? response = null;

            if (employee.ExistsInTrackTik)
            {
                response = await _trackTik.UpdateEmployeeAsync(employee.TrackTikEmployeeId, payload, cancellationToken);
                if (response is null)
                {
                    // Deleted on the TrackTik side since our last sync: fall back to create.
                    LogMissingUpstream(employee.TrackTikEmployeeId);
                    employee.MarkMissingInTrackTik();
                }
            }

            response ??= await _trackTik.CreateEmployeeAsync(payload, cancellationToken);
            employee.MarkSynced(response.Id, _time.GetUtcNow());
        }
        catch (TrackTikApiException ex)
        {
            LogSyncFailed(ex, employee.Provider.ToRouteValue(), employee.ProviderEmployeeId, employee.Attempts);
            employee.MarkFailed(ex.Message, _time.GetUtcNow());
        }

        // The outcome must be stored even if the caller has gone away.
        await _unitOfWork.SaveChangesAsync(CancellationToken.None);

        await PublishEventsAsync(employee, cancellationToken);
    }

    /// <summary>
    /// Runs after the commit, so EmployeeSynced is only ever published for a stored "synced" state.
    /// A publish failure is logged but does not fail the request (see README trade-offs: no outbox).
    /// </summary>
    private async Task PublishEventsAsync(Employee employee, CancellationToken cancellationToken)
    {
        var events = employee.DomainEvents.ToArray();
        employee.ClearDomainEvents();

        foreach (var domainEvent in events)
        {
            try
            {
                await _eventPublisher.PublishAsync(domainEvent, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogPublishFailed(ex, domainEvent.GetType().Name);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Sync to TrackTik failed for {Provider}/{ProviderEmployeeId} (attempt {Attempt})")]
    private partial void LogSyncFailed(Exception ex, string provider, string providerEmployeeId, int attempt);

    [LoggerMessage(Level = LogLevel.Information, Message = "TrackTik returned 404 for {TrackTikEmployeeId}; creating it again")]
    private partial void LogMissingUpstream(string trackTikEmployeeId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Publishing {EventType} failed")]
    private partial void LogPublishFailed(Exception ex, string eventType);
}
