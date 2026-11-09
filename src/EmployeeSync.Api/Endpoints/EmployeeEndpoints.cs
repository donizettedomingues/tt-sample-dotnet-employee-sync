using System.Text.Json;
using EmployeeSync.Application.Abstractions.Messaging;
using EmployeeSync.Application.Employees;
using EmployeeSync.Application.Employees.Commands;
using EmployeeSync.Application.Employees.Queries;
using Microsoft.AspNetCore.Mvc;

namespace EmployeeSync.Api.Endpoints;

/// <summary>Thin HTTP adapter: translate HTTP to commands/queries and results to status codes.</summary>
public static class EmployeeEndpoints
{
    public const string GetEmployeeRouteName = "GetEmployee";
    private const string ProblemTypeBase = "https://tracktik.com/problems/employee-sync/";

    public static IEndpointRouteBuilder MapEmployeeEndpoints(this IEndpointRouteBuilder app)
    {
        // URL-segment versioning: v2 would be a new group next to this one.
        var v1 = app.MapGroup("/api/v1").WithTags("Employees");

        v1.MapPost("/providers/{provider}/employees", SubmitEmployeeAsync)
            .WithName("SubmitEmployee")
            .WithSummary("Receive an employee from a provider, store it and sync it to TrackTik.")
            .WithDescription("provider is provider1 or provider2; the body follows that provider's schema. " +
                             "Resubmitting the same employee updates it and retries the sync.")
            .Accepts<JsonElement>("application/json")
            .Produces<EmployeeDto>(StatusCodes.Status201Created)
            .Produces<EmployeeDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status502BadGateway);

        v1.MapGet("/employees/{provider}/{providerEmployeeId}", GetEmployeeAsync)
            .WithName(GetEmployeeRouteName)
            .WithSummary("Get a stored employee and its TrackTik sync status.")
            .Produces<EmployeeDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> SubmitEmployeeAsync(
        string provider,
        [FromBody] JsonElement payload,
        ICommandHandler<SubmitEmployeeCommand, SubmitEmployeeResult> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new SubmitEmployeeCommand(provider, payload), cancellationToken);

        return result.Outcome switch
        {
            SubmitEmployeeOutcome.UnknownProvider => Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Unknown provider",
                detail: $"Provider '{provider}' is not supported. Use provider1 or provider2.",
                type: ProblemTypeBase + "unknown-provider"),

            SubmitEmployeeOutcome.Invalid => Results.ValidationProblem(
                result.Errors!.ToDictionary(kv => kv.Key, kv => kv.Value),
                title: "The employee payload is invalid",
                type: ProblemTypeBase + "validation"),

            SubmitEmployeeOutcome.SyncFailed => Results.Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "TrackTik sync failed",
                detail: "The employee was stored but could not be forwarded to TrackTik. Resubmit it to retry.",
                type: ProblemTypeBase + "tracktik-sync-failed",
                extensions: new Dictionary<string, object?>
                {
                    ["syncStatus"] = result.Employee!.SyncStatus,
                    ["attempts"] = result.Employee.Attempts,
                    ["lastError"] = result.Employee.LastError,
                    ["lastAttemptAt"] = result.Employee.LastAttemptAt,
                    ["employee"] = result.Employee,
                }),

            SubmitEmployeeOutcome.Synced when result.Created => Results.CreatedAtRoute(
                GetEmployeeRouteName,
                new { provider = result.Employee!.Provider, providerEmployeeId = result.Employee.ProviderEmployeeId },
                result.Employee),

            SubmitEmployeeOutcome.Synced => Results.Ok(result.Employee),

            _ => throw new InvalidOperationException($"Unhandled outcome {result.Outcome}."),
        };
    }

    private static async Task<IResult> GetEmployeeAsync(
        string provider,
        string providerEmployeeId,
        IQueryHandler<GetEmployeeQuery, EmployeeDto?> handler,
        CancellationToken cancellationToken)
    {
        var employee = await handler.HandleAsync(new GetEmployeeQuery(provider, providerEmployeeId), cancellationToken);

        return employee is null
            ? Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Employee not found",
                detail: $"No employee '{providerEmployeeId}' from provider '{provider}'.",
                type: ProblemTypeBase + "not-found")
            : Results.Ok(employee);
    }
}
