using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EmployeeSync.Infrastructure.Persistence;

/// <summary>
/// Lets <c>dotnet ef migrations add</c> build the context without starting the API (and without TrackTik settings).
/// The connection string is only used by <c>dotnet ef database update</c>; override it with
/// <c>ConnectionStrings__EmployeeSync</c>.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<EmployeeSyncDbContext>
{
    public EmployeeSyncDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__EmployeeSync")
            ?? "Server=localhost,1433;Database=EmployeeSync;User Id=sa;Password=placeholder;TrustServerCertificate=True";

        var options = new DbContextOptionsBuilder<EmployeeSyncDbContext>()
            .UseSqlServer(connectionString, sql => sql.MigrationsAssembly(typeof(EmployeeSyncDbContext).Assembly.FullName))
            .Options;

        return new EmployeeSyncDbContext(options);
    }
}
