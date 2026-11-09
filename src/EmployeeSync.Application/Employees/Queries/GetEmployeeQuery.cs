using EmployeeSync.Application.Abstractions.Messaging;
using EmployeeSync.Application.Abstractions.Persistence;
using EmployeeSync.Domain.Employees;

namespace EmployeeSync.Application.Employees.Queries;

/// <summary>Returns the stored employee and its sync status, or null if unknown.</summary>
public sealed record GetEmployeeQuery(string Provider, string ProviderEmployeeId) : IQuery<EmployeeDto?>;

public sealed class GetEmployeeHandler : IQueryHandler<GetEmployeeQuery, EmployeeDto?>
{
    private readonly IEmployeeReadStore _readStore;

    public GetEmployeeHandler(IEmployeeReadStore readStore) => _readStore = readStore;

    public Task<EmployeeDto?> HandleAsync(GetEmployeeQuery query, CancellationToken cancellationToken)
    {
        if (!ProviderNames.TryParse(query.Provider, out var provider) || string.IsNullOrWhiteSpace(query.ProviderEmployeeId))
        {
            return Task.FromResult<EmployeeDto?>(null);
        }

        return _readStore.GetAsync(provider, query.ProviderEmployeeId.Trim(), cancellationToken);
    }
}
