using System.Collections.Concurrent;
using System.Net;
using System.Text;

namespace EmployeeSync.IntegrationTests;

/// <summary>
/// In-process stand-in for the TrackTik mock API (token endpoint + employees), plugged in as the
/// primary HttpMessageHandler of both HttpClients. Lets tests simulate outages the real mock can't.
/// </summary>
public sealed class FakeTrackTikApi : HttpMessageHandler
{
    public const string TokenUrl = "https://tracktik.test/oauth/token";
    public const string ApiBaseUrl = "https://tracktik.test/v1";
    private const string Token = "fake-access-token";

    public ConcurrentQueue<(HttpMethod Method, string Path, string? Body)> Requests { get; } = new();

    /// <summary>When set, employee endpoints answer with this status.</summary>
    public HttpStatusCode? EmployeeFailure { get; set; }

    /// <summary>When set, the token endpoint answers with this status.</summary>
    public HttpStatusCode? TokenFailure { get; set; }

    public int TokenRequests => Requests.Count(r => r.Path == "/oauth/token");

    public IReadOnlyList<(HttpMethod Method, string Path, string? Body)> EmployeeRequests =>
        Requests.Where(r => r.Path.StartsWith("/v1/employees", StringComparison.Ordinal)).ToList();

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath;
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Enqueue((request.Method, path, body));

        if (path == "/oauth/token")
        {
            return TokenFailure is { } tokenStatus
                ? Json(tokenStatus, """{"error":"invalid_client"}""")
                : Json(HttpStatusCode.OK, $$"""{"access_token":"{{Token}}","token_type":"Bearer","expires_in":3600}""");
        }

        if (request.Headers.Authorization?.ToString() != $"Bearer {Token}")
        {
            return Json(HttpStatusCode.Unauthorized, """{"success":false,"error":{"code":"UNAUTHORIZED","message":"Invalid or missing authorization token"}}""");
        }

        if (EmployeeFailure is { } status)
        {
            return Json(status, """{"success":false,"error":{"code":"INTERNAL_ERROR","message":"Internal server error"}}""");
        }

        if (request.Method == HttpMethod.Post && path == "/v1/employees")
        {
            return Json(HttpStatusCode.Created, $$$"""{"success":true,"data":{"id":"{{{Guid.NewGuid()}}}"}}""");
        }

        if (request.Method == HttpMethod.Put && path.StartsWith("/v1/employees/", StringComparison.Ordinal))
        {
            return Json(HttpStatusCode.OK, $$$"""{"success":true,"data":{"id":"{{{Guid.NewGuid()}}}"}}""");
        }

        return Json(HttpStatusCode.NotFound, """{"success":false,"error":{"code":"NOT_FOUND","message":"Endpoint not found"}}""");
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
}
