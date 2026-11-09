using System.Net;
using System.Text.Json;
using EmployeeSync.Application.Abstractions.TrackTik;
using EmployeeSync.Application.Employees.Commands;
using EmployeeSync.Application.Employees.Mapping;
using EmployeeSync.Application.Employees.Sync;
using EmployeeSync.Domain.Employees;
using EmployeeSync.UnitTests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace EmployeeSync.UnitTests.Employees;

public class SubmitEmployeeHandlerTests
{
    private const string Alice = """
        { "emp_id": "P1_001", "first_name": "Alice", "last_name": "Johnson", "email_address": "alice.johnson@provider1.com" }
        """;

    private readonly InMemoryEmployeeStore _store = new();
    private readonly FakeTrackTikClient _trackTik;
    private readonly RecordingEventPublisher _events = new();
    private readonly MutableTimeProvider _time = new(new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero));
    private readonly SubmitEmployeeHandler _handler;

    public SubmitEmployeeHandlerTests()
    {
        _trackTik = new FakeTrackTikClient(_store);
        var sync = new EmployeeSyncService(_trackTik, _store, _events, _time, NullLogger<EmployeeSyncService>.Instance);
        var mappers = new ProviderMapperResolver([new Provider1EmployeeMapper(), new Provider2EmployeeMapper()]);
        _handler = new SubmitEmployeeHandler(mappers, _store, _store, sync, _time);
    }

    private Task<SubmitEmployeeResult> Submit(string json, string provider = "provider1") =>
        _handler.HandleAsync(new SubmitEmployeeCommand(provider, JsonDocument.Parse(json).RootElement), CancellationToken.None);

    [Fact]
    public async Task New_employee_is_persisted_pending_then_posted_and_marked_synced()
    {
        var result = await Submit(Alice);

        Assert.Equal(SubmitEmployeeOutcome.Synced, result.Outcome);
        Assert.True(result.Created);
        Assert.Equal("synced", result.Employee!.SyncStatus);
        Assert.Equal(1, result.Employee.Attempts);
        Assert.Equal(_time.Now, result.Employee.LastAttemptAt);
        Assert.Null(result.Employee.LastError);

        var call = Assert.Single(_trackTik.Calls);
        Assert.Equal("POST", call.Method);
        Assert.Equal("provider1-P1_001", call.Payload.EmployeeId);

        // The record was committed as "pending" before TrackTik was called.
        Assert.Equal(SyncStatus.Pending, _trackTik.StatusSeenAtCall.Single());
        Assert.Equal([SyncStatus.Pending, SyncStatus.Synced], _store.CommittedStatuses);
    }

    [Fact]
    public async Task Successful_sync_publishes_one_EmployeeSynced_after_commit()
    {
        await Submit(Alice);

        var evt = Assert.IsType<EmployeeSynced>(Assert.Single(_events.Published));
        Assert.Equal("provider1", evt.Provider);
        Assert.Equal("P1_001", evt.ProviderEmployeeId);
        Assert.Equal("provider1-P1_001", evt.TrackTikEmployeeId);
        Assert.Equal(SyncStatus.Synced, _store.CommittedStatuses.Last());
    }

    [Fact]
    public async Task Upstream_failure_keeps_the_record_as_failed_with_error_and_no_event()
    {
        _trackTik.FailWith = new TrackTikApiException("TrackTik POST /employees returned HTTP 503", HttpStatusCode.ServiceUnavailable);

        var result = await Submit(Alice);

        Assert.Equal(SubmitEmployeeOutcome.SyncFailed, result.Outcome);
        Assert.Equal("failed", result.Employee!.SyncStatus);
        Assert.Equal(1, result.Employee.Attempts);
        Assert.Contains("503", result.Employee.LastError);
        Assert.Empty(_events.Published);

        // Not rolled back: the employee is still stored.
        var stored = Assert.Single(_store.Committed);
        Assert.Equal(SyncStatus.Failed, stored.SyncStatus);
        Assert.Equal("Alice", stored.FirstName);
    }

    [Fact]
    public async Task Resubmitting_a_failed_employee_retries_with_POST_and_updates_in_place()
    {
        _trackTik.FailWith = new TrackTikApiException("boom");
        await Submit(Alice);

        _trackTik.FailWith = null;
        _time.Advance(TimeSpan.FromMinutes(5));
        var result = await Submit(Alice.Replace("Johnson", "Johnson-Smith"));

        Assert.Equal(SubmitEmployeeOutcome.Synced, result.Outcome);
        Assert.False(result.Created);
        Assert.Equal(2, result.Employee!.Attempts);
        Assert.Equal(_time.Now, result.Employee.LastAttemptAt);
        Assert.Null(result.Employee.LastError);
        Assert.Equal("Johnson-Smith", result.Employee.LastName);

        Assert.Single(_store.Committed); // never a duplicate
        Assert.Equal(["POST", "POST"], _trackTik.Calls.Select(c => c.Method));
        Assert.Single(_events.Published);
    }

    [Fact]
    public async Task Resubmitting_a_synced_employee_uses_PUT()
    {
        await Submit(Alice);
        var result = await Submit(Alice.Replace("Alice", "Alicia"));

        Assert.Equal(["POST", "PUT"], _trackTik.Calls.Select(c => c.Method));
        Assert.Equal("Alicia", _trackTik.Calls[1].Payload.FirstName);
        Assert.Equal(2, result.Employee!.Attempts);
        Assert.Equal(2, _events.Published.Count); // once per successful sync
    }

    [Fact]
    public async Task PUT_returning_404_falls_back_to_POST()
    {
        await Submit(Alice);
        _trackTik.UpdateReturnsNotFound = true;

        var result = await Submit(Alice);

        Assert.Equal(SubmitEmployeeOutcome.Synced, result.Outcome);
        Assert.Equal(["POST", "PUT", "POST"], _trackTik.Calls.Select(c => c.Method));
    }

    [Fact]
    public async Task Failed_PUT_keeps_PUT_for_the_next_retry()
    {
        await Submit(Alice);
        _trackTik.FailWith = new TrackTikApiException("timeout");
        await Submit(Alice);
        _trackTik.FailWith = null;

        await Submit(Alice);

        Assert.Equal(["POST", "PUT", "PUT"], _trackTik.Calls.Select(c => c.Method));
    }

    [Fact]
    public async Task Invalid_payload_is_rejected_and_nothing_is_stored_or_sent()
    {
        var result = await Submit("""{ "emp_id": "P1_001", "email_address": "nope" }""");

        Assert.Equal(SubmitEmployeeOutcome.Invalid, result.Outcome);
        Assert.NotEmpty(result.Errors!);
        Assert.Empty(_store.Committed);
        Assert.Empty(_trackTik.Calls);
    }

    [Fact]
    public async Task Unknown_provider_is_rejected()
    {
        var result = await Submit(Alice, provider: "provider3");

        Assert.Equal(SubmitEmployeeOutcome.UnknownProvider, result.Outcome);
        Assert.Empty(_store.Committed);
    }

    [Fact]
    public async Task Same_id_from_different_providers_are_separate_records()
    {
        await Submit("""{ "emp_id": "001", "first_name": "A", "last_name": "B", "email_address": "same@x.co" }""");
        await Submit("""
            { "employee_number": "001", "personal_info": { "given_name": "A", "family_name": "B", "email": "same@x.co" }, "work_info": {} }
            """, provider: "provider2");

        Assert.Equal(2, _store.Committed.Count);
        Assert.Equal(["provider1-001", "provider2-001"], _trackTik.Calls.Select(c => c.Payload.EmployeeId));
    }

    [Fact]
    public async Task Event_publish_failure_does_not_fail_the_sync()
    {
        _events.Throw = true;

        var result = await Submit(Alice);

        Assert.Equal(SubmitEmployeeOutcome.Synced, result.Outcome);
    }
}
