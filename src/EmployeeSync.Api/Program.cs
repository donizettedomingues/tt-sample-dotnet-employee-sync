using EmployeeSync.Api.Endpoints;
using EmployeeSync.Application;
using EmployeeSync.Infrastructure;
using EmployeeSync.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi("v1", options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info.Title = "Employee Sync API";
        document.Info.Version = "v1";
        document.Info.Description = "Receives employees from HR providers, stores them and syncs them to TrackTik.";
        return Task.CompletedTask;
    });
});

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

builder.Services.AddHealthChecks()
    .AddDbContextCheck<EmployeeSyncDbContext>("database");

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await app.Services.MigrateDatabaseAsync();
}

// OpenAPI document at /openapi/v1.json
app.MapOpenApi();
app.MapHealthChecks("/health");
app.MapEmployeeEndpoints();

await app.RunAsync();

/// <summary>Exposed for WebApplicationFactory in the integration tests.</summary>
public partial class Program;
