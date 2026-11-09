using EmployeeSync.Application.Abstractions.Messaging;
using EmployeeSync.Application.Abstractions.Persistence;
using EmployeeSync.Application.Employees.Mapping;
using EmployeeSync.Application.Employees.Sync;
using EmployeeSync.Domain.Employees;

namespace EmployeeSync.Application.Employees.Commands;

/// <summary>
/// Validate -> map -> persist as <c>pending</c> -> forward to TrackTik -> <c>synced</c>/<c>failed</c>.
/// A resubmission with the same (provider, providerEmployeeId) updates the record and retries the sync.
/// </summary>
public sealed class SubmitEmployeeHandler : ICommandHandler<SubmitEmployeeCommand, SubmitEmployeeResult>
{
    private readonly ProviderMapperResolver _mappers;
    private readonly IEmployeeRepository _employees;
    private readonly IUnitOfWork _unitOfWork;
    private readonly EmployeeSyncService _syncService;
    private readonly TimeProvider _time;

    public SubmitEmployeeHandler(
        ProviderMapperResolver mappers,
        IEmployeeRepository employees,
        IUnitOfWork unitOfWork,
        EmployeeSyncService syncService,
        TimeProvider time)
    {
        _mappers = mappers;
        _employees = employees;
        _unitOfWork = unitOfWork;
        _syncService = syncService;
        _time = time;
    }

    public async Task<SubmitEmployeeResult> HandleAsync(SubmitEmployeeCommand command, CancellationToken cancellationToken)
    {
        if (!ProviderNames.TryParse(command.Provider, out var provider))
        {
            return new SubmitEmployeeResult(SubmitEmployeeOutcome.UnknownProvider);
        }

        // 1. Validate + map. Nothing is stored for an invalid payload.
        var mapping = _mappers.Resolve(provider).Map(command.Payload);
        if (!mapping.IsValid)
        {
            return new SubmitEmployeeResult(SubmitEmployeeOutcome.Invalid, Errors: mapping.Errors);
        }

        // 2. Upsert by identity and commit as "pending" before calling TrackTik.
        var now = _time.GetUtcNow();
        var employee = await _employees.FindAsync(provider, mapping.ProviderEmployeeId!, cancellationToken);
        var created = employee is null;

        if (employee is null)
        {
            employee = Employee.Create(provider, mapping.ProviderEmployeeId!, mapping.Details!, now);
            _employees.Add(employee);
        }
        else
        {
            employee.ApplyDetails(mapping.Details!, now);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // 3. Forward and record the outcome (never rolls back the stored data).
        await _syncService.SyncAsync(employee, cancellationToken);

        var outcome = employee.SyncStatus == SyncStatus.Synced
            ? SubmitEmployeeOutcome.Synced
            : SubmitEmployeeOutcome.SyncFailed;

        return new SubmitEmployeeResult(outcome, EmployeeDto.From(employee), created);
    }
}
