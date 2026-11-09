using EmployeeSync.Application.Abstractions.Persistence;
using EmployeeSync.Domain.Employees;
using Microsoft.EntityFrameworkCore;

namespace EmployeeSync.Infrastructure.Persistence;

public sealed class EmployeeSyncDbContext : DbContext, IUnitOfWork
{
    public EmployeeSyncDbContext(DbContextOptions<EmployeeSyncDbContext> options)
        : base(options)
    {
    }

    public DbSet<Employee> Employees => Set<Employee>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EmployeeSyncDbContext).Assembly);

    Task IUnitOfWork.SaveChangesAsync(CancellationToken cancellationToken) => SaveChangesAsync(cancellationToken);
}
