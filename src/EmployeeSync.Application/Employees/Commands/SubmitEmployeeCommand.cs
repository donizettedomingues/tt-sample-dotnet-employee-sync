using System.Text.Json;
using EmployeeSync.Application.Abstractions.Messaging;

namespace EmployeeSync.Application.Employees.Commands;

/// <summary>A provider pushed one employee in its own JSON shape.</summary>
public sealed record SubmitEmployeeCommand(string Provider, JsonElement Payload) : ICommand<SubmitEmployeeResult>;

public enum SubmitEmployeeOutcome
{
    /// <summary>Stored and forwarded to TrackTik.</summary>
    Synced,

    /// <summary>Stored, but the forward to TrackTik failed (record is <c>failed</c>).</summary>
    SyncFailed,

    /// <summary>Payload failed validation; nothing was stored.</summary>
    Invalid,

    /// <summary>The provider in the route is not supported; nothing was stored.</summary>
    UnknownProvider,
}

public sealed record SubmitEmployeeResult(
    SubmitEmployeeOutcome Outcome,
    EmployeeDto? Employee = null,
    bool Created = false,
    IReadOnlyDictionary<string, string[]>? Errors = null);
