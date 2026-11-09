using System.Net;
using EmployeeSync.Application.Abstractions.TrackTik;
using EmployeeSync.Infrastructure.TrackTik;
using EmployeeSync.UnitTests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace EmployeeSync.UnitTests.TrackTik;

public class TrackTikAccessTokenProviderTests
{
    private readonly MutableTimeProvider _time = new(DateTimeOffset.UnixEpoch);
    private int _issued;

    private static readonly TrackTikOptions Options = new()
    {
        TokenUrl = "https://tracktik.test/oauth/token",
        ApiBaseUrl = "https://tracktik.test/v1",
        ClientId = "client",
        ClientSecret = "secret",
        TokenRefreshSkewSeconds = 60,
    };

    private (TrackTikAccessTokenProvider Provider, StubHttpMessageHandler Handler) Create(HttpStatusCode status = HttpStatusCode.OK)
    {
        var handler = new StubHttpMessageHandler((_, _) => status == HttpStatusCode.OK
            ? StubHttpMessageHandler.Json(status, $$"""{"access_token":"token-{{++_issued}}","token_type":"Bearer","expires_in":3600}""")
            : StubHttpMessageHandler.Json(status, """{"error":"invalid_client"}"""));

        var provider = new TrackTikAccessTokenProvider(
            new StubHttpClientFactory(handler),
            new StaticOptionsMonitor(Options),
            _time,
            NullLogger<TrackTikAccessTokenProvider>.Instance);
        return (provider, handler);
    }

    [Fact]
    public async Task Requests_a_client_credentials_token_without_scope()
    {
        var (provider, handler) = Create();

        var token = await provider.GetAccessTokenAsync(CancellationToken.None);

        Assert.Equal("token-1", token);
        var (request, body) = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(Options.TokenUrl, request.RequestUri!.ToString());
        Assert.Equal("grant_type=client_credentials&client_id=client&client_secret=secret", body);
    }

    [Fact]
    public async Task Caches_the_token_until_shortly_before_expiry()
    {
        var (provider, handler) = Create();

        await provider.GetAccessTokenAsync(CancellationToken.None);
        _time.Advance(TimeSpan.FromSeconds(3600 - 61));
        var stillCached = await provider.GetAccessTokenAsync(CancellationToken.None);
        _time.Advance(TimeSpan.FromSeconds(2));
        var refreshed = await provider.GetAccessTokenAsync(CancellationToken.None);

        Assert.Equal("token-1", stillCached);
        Assert.Equal("token-2", refreshed);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task Concurrent_callers_share_one_token_request()
    {
        var (provider, handler) = Create();

        var tokens = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => provider.GetAccessTokenAsync(CancellationToken.None)));

        Assert.All(tokens, t => Assert.Equal("token-1", t));
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Invalidate_forces_a_new_token()
    {
        var (provider, handler) = Create();
        await provider.GetAccessTokenAsync(CancellationToken.None);

        provider.Invalidate();
        await provider.GetAccessTokenAsync(CancellationToken.None);

        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task Token_endpoint_error_becomes_TrackTikApiException_without_leaking_secret()
    {
        var (provider, _) = Create(HttpStatusCode.Unauthorized);

        var ex = await Assert.ThrowsAsync<TrackTikApiException>(() => provider.GetAccessTokenAsync(CancellationToken.None));

        Assert.Equal(HttpStatusCode.Unauthorized, ex.StatusCode);
        Assert.DoesNotContain("secret", ex.Message);
    }

    private sealed class StaticOptionsMonitor(TrackTikOptions value) : IOptionsMonitor<TrackTikOptions>
    {
        public TrackTikOptions CurrentValue => value;

        public TrackTikOptions Get(string? name) => value;

        public IDisposable? OnChange(Action<TrackTikOptions, string?> listener) => null;
    }
}
