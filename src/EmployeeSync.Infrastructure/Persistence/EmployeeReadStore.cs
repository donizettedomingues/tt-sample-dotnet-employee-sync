using EmployeeSync.Application.Abstractions.Persistence;
using EmployeeSync.Application.Employees;
using EmployeeSync.Domain.Employees;
using Microsoft.EntityFrameworkCore;

namespace EmployeeSync.Infrastructure.Persistence;

internal sealed class EmployeeReadStore : IEmployeeReadStore
{
    private readonly EmployeeSyncDbContext _db;

    public EmployeeReadStore(EmployeeSyncDbContext db) => _db = db;

    public async Task<EmployeeDto?> GetAsync(Provider provider, string providerEmployeeId, CancellationToken cancellationToken)
    {
        var employee = await _db.Employees
            .AsNoTracking()
            .SingleOrDefaultAsync(e => e.Provider == provider && e.ProviderEmployeeId == providerEmployeeId, cancellationToken);

        return employee is null ? null : EmployeeDto.From(employee);
    }
}
