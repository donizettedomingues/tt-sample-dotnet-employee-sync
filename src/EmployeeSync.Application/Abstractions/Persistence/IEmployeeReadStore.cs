using EmployeeSync.Application.Employees;
using EmployeeSync.Domain.Employees;

namespace EmployeeSync.Application.Abstractions.Persistence;

/// <summary>Read-side access used by queries (no change tracking, returns DTOs).</summary>
public interface IEmployeeReadStore
{
    Task<EmployeeDto?> GetAsync(Provider provider, string providerEmployeeId, CancellationToken cancellationToken);
}
