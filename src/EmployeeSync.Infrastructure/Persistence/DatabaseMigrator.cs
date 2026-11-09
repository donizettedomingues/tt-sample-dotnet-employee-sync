using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EmployeeSync.Infrastructure.Persistence;

public static class DatabaseMigrator
{
    /// <summary>Applies pending EF Core migrations. Used by the API on startup when Database:MigrateOnStartup is true.</summary>
    public static async Task MigrateDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<EmployeeSyncDbContext>();
        await db.Database.MigrateAsync(cancellationToken);
    }
}
