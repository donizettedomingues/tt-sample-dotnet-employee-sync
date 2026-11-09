using System.Net;
using System.Text.Json;
using EmployeeSync.Application.Abstractions.TrackTik;
using EmployeeSync.Infrastructure.TrackTik;
using EmployeeSync.UnitTests.Fakes;

namespace EmployeeSync.UnitTests.TrackTik;

public class TrackTikClientTests
{
    private static readonly TrackTikEmployee Alice =
        new("provider1-P1_001", "Alice", "Johnson", "alice@x.co", null, "Officer", null, "2024-01-15", "active");

    private static (TrackTikClient Client, StubHttpMessageHandler Handler) Create(Func<HttpRequestMessage, string?, HttpResponseMessage> respond)
    {
        var handler = new StubHttpMessageHandler(respond);
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://tracktik.test/v1/") };
        return (new TrackTikClient(http), handler);
    }

    [Fact]
    public async Task Create_posts_camelCase_json_under_v1_and_reads_the_id()
    {
        var (client, handler) = Create((_, _) => StubHttpMessageHandler.Json(HttpStatusCode.Created,
            """{"success":true,"data":{"id":"uuid-1","employeeId":"provider1-P1_001"}}"""));

        var response = await client.CreateEmployeeAsync(Alice, CancellationToken.None);

        Assert.Equal("uuid-1", response.Id);
        var (request, body) = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://tracktik.test/v1/employees", request.RequestUri!.ToString());
        using var json = JsonDocument.Parse(body!);
        Assert.Equal("provider1-P1_001", json.RootElement.GetProperty("employeeId").GetString());
        Assert.False(json.RootElement.TryGetProperty("phoneNumber", out _)); // nulls omitted
    }

    [Fact]
    public async Task Update_puts_to_the_employee_id_and_returns_null_on_404()
    {
        var (client, handler) = Create((_, _) => StubHttpMessageHandler.Json(HttpStatusCode.NotFound, "{}"));

        var response = await client.UpdateEmployeeAsync("provider1-P1_001", Alice, CancellationToken.None);

        Assert.Null(response);
        Assert.Equal(HttpMethod.Put, handler.Requests[0].Request.Method);
        Assert.Equal("https://tracktik.test/v1/employees/provider1-P1_001", handler.Requests[0].Request.RequestUri!.ToString());
    }

    [Fact]
    public async Task Error_status_becomes_TrackTikApiException_with_the_upstream_message()
    {
        var (client, _) = Create((_, _) => StubHttpMessageHandler.Json(HttpStatusCode.BadRequest,
            """{"success":false,"error":{"code":"VALIDATION_ERROR","message":"Invalid employee data"}}"""));

        var ex = await Assert.ThrowsAsync<TrackTikApiException>(() => client.CreateEmployeeAsync(Alice, CancellationToken.None));

        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
        Assert.Equal("TrackTik POST /employees returned HTTP 400: Invalid employee data", ex.Message);
    }

    [Fact]
    public async Task Transport_error_becomes_TrackTikApiException()
    {
        var (client, _) = Create((_, _) => throw new HttpRequestException("connection refused"));

        var ex = await Assert.ThrowsAsync<TrackTikApiException>(() => client.CreateEmployeeAsync(Alice, CancellationToken.None));

        Assert.Contains("unreachable", ex.Message);
    }

    [Fact]
    public async Task Auth_handler_adds_bearer_and_retries_once_with_a_new_token_after_401()
    {
        var tokens = new CountingTokenProvider();
        var calls = 0;
        var inner = new StubHttpMessageHandler((req, _) => ++calls == 1
            ? new HttpResponseMessage(HttpStatusCode.Unauthorized)
            : StubHttpMessageHandler.Json(HttpStatusCode.Created, """{"success":true,"data":{"id":"x"}}"""));
        var http = new HttpClient(new TrackTikAuthenticationHandler(tokens) { InnerHandler = inner })
        {
            BaseAddress = new Uri("https://tracktik.test/v1/"),
        };

        await new TrackTikClient(http).CreateEmployeeAsync(Alice, CancellationToken.None);

        Assert.Equal(2, calls);
        Assert.Equal(1, tokens.Invalidations);
        Assert.Equal("Bearer token-2", inner.Requests[1].Request.Headers.Authorization!.ToString());
    }

    private sealed class CountingTokenProvider : ITrackTikAccessTokenProvider
    {
        private int _issued;

        public int Invalidations { get; private set; }

        public Task<string> GetAccessTokenAsync(CancellationToken cancellationToken) =>
            Task.FromResult($"token-{(Invalidations == 0 ? 1 : ++_issued + 1)}");

        public void Invalidate() => Invalidations++;
    }
}
