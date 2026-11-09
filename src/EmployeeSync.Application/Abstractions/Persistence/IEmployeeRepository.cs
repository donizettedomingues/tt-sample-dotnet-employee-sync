using EmployeeSync.Domain.Employees;

namespace EmployeeSync.Application.Abstractions.Persistence;

/// <summary>Write-side access to the Employee aggregate.</summary>
public interface IEmployeeRepository
{
    Task<Employee?> FindAsync(Provider provider, string providerEmployeeId, CancellationToken cancellationToken);

    void Add(Employee employee);
}
