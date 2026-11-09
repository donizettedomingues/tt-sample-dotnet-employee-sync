using EmployeeSync.Infrastructure.Persistence;
using EmployeeSync.Infrastructure.TrackTik;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EmployeeSync.IntegrationTests;

/// <summary>
/// Boots the real API in memory. Two substitutions, both documented in the README:
///  - SQL Server -> SQLite in-memory (same EF model; created with EnsureCreated, not the SQL Server migrations);
///  - TrackTik -> <see cref="FakeTrackTikApi"/> as the primary HTTP handler, so OAuth + typed client run for real.
/// </summary>
public sealed class EmployeeSyncApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public FakeTrackTikApi TrackTik { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // UseSetting is applied before Program reads configuration.
        builder.UseSetting("ConnectionStrings:EmployeeSync", "Server=not-used-in-tests");
        builder.UseSetting("Database:MigrateOnStartup", "false");
        builder.UseSetting("TrackTik:TokenUrl", FakeTrackTikApi.TokenUrl);
        builder.UseSetting("TrackTik:ApiBaseUrl", FakeTrackTikApi.ApiBaseUrl);
        builder.UseSetting("TrackTik:ClientId", "test-client");
        builder.UseSetting("TrackTik:ClientSecret", "test-secret");
        builder.UseSetting("Events:Publisher", "InMemory");

        builder.ConfigureTestServices(services =>
        {
            _connection.Open();
            services.RemoveAll<DbContextOptions<EmployeeSyncDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<EmployeeSyncDbContext>>();
            services.AddDbContext<EmployeeSyncDbContext>(o => o.UseSqlite(_connection));

            services.AddHttpClient(TrackTikAccessTokenProvider.HttpClientName)
                .ConfigurePrimaryHttpMessageHandler(() => TrackTik);
            services.AddHttpClient(TrackTikClient.HttpClientName)
                .ConfigurePrimaryHttpMessageHandler(() => TrackTik);
        });
    }

    protected override void ConfigureClient(HttpClient client)
    {
        base.ConfigureClient(client);
        using var scope = Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<EmployeeSyncDbContext>().Database.EnsureCreated();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
        }
    }
}
