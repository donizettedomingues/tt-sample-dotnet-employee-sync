using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using EmployeeSync.Domain.Employees;
using EmployeeSync.Infrastructure.Events;
using Microsoft.Extensions.DependencyInjection;

namespace EmployeeSync.IntegrationTests;

public sealed class EmployeeEndpointsTests : IDisposable
{
    private const string Alice = """
        {
          "emp_id": "P1_001", "first_name": "Alice", "last_name": "Johnson",
          "email_address": "alice.johnson@provider1.com", "phone": "+1-555-0101",
          "job_title": "Security Officer", "dept": "Security Operations",
          "hire_date": "2024-01-15", "employment_status": "active"
        }
        """;

    private const string Carol = """
        {
          "employee_number": "P2_001",
          "personal_info": { "given_name": "Carol", "family_name": "Davis", "email": "carol.davis@provider2.com", "mobile": "+1-555-0201" },
          "work_info": { "role": "Security Guard", "division": "Night Shift Security", "start_date": "2024-02-01", "current_status": "on_leave" }
        }
        """;

    private readonly EmployeeSyncApiFactory _factory = new();
    private readonly HttpClient _client;

    public EmployeeEndpointsTests() => _client = _factory.CreateClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private Task<HttpResponseMessage> Post(string provider, string json) =>
        _client.PostAsync($"/api/v1/providers/{provider}/employees", new StringContent(json, Encoding.UTF8, "application/json"));

    private static async Task<JsonElement> Body(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>());

    [Fact]
    public async Task Post_new_employee_returns_201_synced_and_GET_returns_it()
    {
        var response = await Post("provider1", Alice);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("/api/v1/employees/provider1/P1_001", response.Headers.Location!.AbsolutePath);
        var created = await Body(response);
        Assert.Equal("synced", created.GetProperty("syncStatus").GetString());
        Assert.Equal("provider1-P1_001", created.GetProperty("trackTikEmployeeId").GetString());

        var get = await _client.GetAsync("/api/v1/employees/provider1/P1_001");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var stored = await Body(get);
        Assert.Equal("Alice", stored.GetProperty("firstName").GetString());
        Assert.Equal("2024-01-15", stored.GetProperty("startDate").GetString());
        Assert.Equal(1, stored.GetProperty("attempts").GetInt32());
        Assert.Equal(JsonValueKind.Null, stored.GetProperty("lastError").ValueKind);

        // TrackTik received the mapped employee.
        var (method, path, body) = Assert.Single(_factory.TrackTik.EmployeeRequests);
        Assert.Equal(HttpMethod.Post, method);
        Assert.Equal("/v1/employees", path);
        using var sent = JsonDocument.Parse(body!);
        Assert.Equal("provider1-P1_001", sent.RootElement.GetProperty("employeeId").GetString());
        Assert.Equal("Security Officer", sent.RootElement.GetProperty("position").GetString());
    }

    [Fact]
    public async Task Post_provider2_maps_nested_payload_and_status()
    {
        var response = await Post("provider2", Carol);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await Body(response);
        Assert.Equal("inactive", body.GetProperty("status").GetString());
        Assert.Equal("provider2-P2_001", body.GetProperty("trackTikEmployeeId").GetString());
    }

    [Fact]
    public async Task Invalid_payload_returns_400_problem_and_stores_nothing()
    {
        var response = await Post("provider1", """{ "emp_id": "P1_404", "email_address": "nope" }""");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        var problem = await Body(response);
        var errors = problem.GetProperty("errors");
        Assert.True(errors.TryGetProperty("email_address", out _));
        Assert.True(errors.TryGetProperty("first_name", out _));

        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/api/v1/employees/provider1/P1_404")).StatusCode);
        Assert.Empty(_factory.TrackTik.Requests);
    }

    [Fact]
    public async Task Malformed_json_returns_400_problem()
    {
        var response = await Post("provider1", "{ not json");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task Unknown_provider_returns_404_problem()
    {
        var response = await Post("provider3", Alice);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Unknown provider", (await Body(response)).GetProperty("title").GetString());
    }

    [Fact]
    public async Task Get_unknown_employee_returns_404_problem()
    {
        var response = await _client.GetAsync("/api/v1/employees/provider1/nobody");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task TrackTik_outage_returns_502_keeps_failed_record_and_resubmission_retries()
    {
        _factory.TrackTik.EmployeeFailure = HttpStatusCode.InternalServerError;

        var failed = await Post("provider1", Alice);

        Assert.Equal(HttpStatusCode.BadGateway, failed.StatusCode);
        var problem = await Body(failed);
        Assert.Equal("failed", problem.GetProperty("syncStatus").GetString());
        Assert.Equal(1, problem.GetProperty("attempts").GetInt32());
        Assert.Contains("HTTP 500", problem.GetProperty("lastError").GetString());

        var stored = await Body(await _client.GetAsync("/api/v1/employees/provider1/P1_001"));
        Assert.Equal("failed", stored.GetProperty("syncStatus").GetString());

        // TrackTik is back: resubmission updates the same record and retries (POST, as it never got created).
        _factory.TrackTik.EmployeeFailure = null;
        var retried = await Post("provider1", Alice.Replace("Johnson", "Johnson-Smith"));

        Assert.Equal(HttpStatusCode.OK, retried.StatusCode);
        var body = await Body(retried);
        Assert.Equal("synced", body.GetProperty("syncStatus").GetString());
        Assert.Equal(2, body.GetProperty("attempts").GetInt32());
        Assert.Equal("Johnson-Smith", body.GetProperty("lastName").GetString());
        Assert.Equal([HttpMethod.Post, HttpMethod.Post], _factory.TrackTik.EmployeeRequests.Select(r => r.Method));
    }

    [Fact]
    public async Task Resubmitting_a_synced_employee_updates_with_PUT_and_reuses_the_token()
    {
        await Post("provider1", Alice);
        var response = await Post("provider1", Alice.Replace("Security Officer", "Senior Security Officer"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var requests = _factory.TrackTik.EmployeeRequests;
        Assert.Equal([HttpMethod.Post, HttpMethod.Put], requests.Select(r => r.Method));
        Assert.Equal("/v1/employees/provider1-P1_001", requests[1].Path);
        Assert.Equal(1, _factory.TrackTik.TokenRequests);
    }

    [Fact]
    public async Task Token_endpoint_failure_returns_502_and_failed_record()
    {
        _factory.TrackTik.TokenFailure = HttpStatusCode.Unauthorized;

        var response = await Post("provider2", Carol);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Contains("token", (await Body(response)).GetProperty("lastError").GetString());
        Assert.Empty(_factory.TrackTik.EmployeeRequests);
    }

    [Fact]
    public async Task EmployeeSynced_is_published_only_for_successful_syncs()
    {
        _factory.TrackTik.EmployeeFailure = HttpStatusCode.ServiceUnavailable;
        await Post("provider1", Alice);
        var publisher = _factory.Services.GetRequiredService<InMemoryEventPublisher>();
        Assert.Empty(publisher.Published);

        _factory.TrackTik.EmployeeFailure = null;
        await Post("provider1", Alice);

        var evt = Assert.IsType<EmployeeSynced>(Assert.Single(publisher.Published));
        Assert.Equal("provider1-P1_001", evt.TrackTikEmployeeId);
        Assert.Equal(2, evt.Attempts);
    }

    [Fact]
    public async Task OpenApi_document_describes_the_versioned_routes()
    {
        var response = await _client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var paths = (await Body(response)).GetProperty("paths");
        Assert.True(paths.TryGetProperty("/api/v1/providers/{provider}/employees", out _));
        Assert.True(paths.TryGetProperty("/api/v1/employees/{provider}/{providerEmployeeId}", out _));
    }
}
