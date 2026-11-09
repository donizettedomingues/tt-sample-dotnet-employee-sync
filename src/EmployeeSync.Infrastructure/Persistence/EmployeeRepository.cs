using EmployeeSync.Application.Abstractions.Persistence;
using EmployeeSync.Domain.Employees;
using Microsoft.EntityFrameworkCore;

namespace EmployeeSync.Infrastructure.Persistence;

internal sealed class EmployeeRepository : IEmployeeRepository
{
    private readonly EmployeeSyncDbContext _db;

    public EmployeeRepository(EmployeeSyncDbContext db) => _db = db;

    public Task<Employee?> FindAsync(Provider provider, string providerEmployeeId, CancellationToken cancellationToken) =>
        _db.Employees.SingleOrDefaultAsync(
            e => e.Provider == provider && e.ProviderEmployeeId == providerEmployeeId,
            cancellationToken);

    public void Add(Employee employee) => _db.Employees.Add(employee);
}
