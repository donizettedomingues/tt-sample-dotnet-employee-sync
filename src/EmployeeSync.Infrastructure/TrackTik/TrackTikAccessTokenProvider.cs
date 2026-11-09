using System.Net.Http.Json;
using System.Text.Json.Serialization;
using EmployeeSync.Application.Abstractions.TrackTik;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EmployeeSync.Infrastructure.TrackTik;

public interface ITrackTikAccessTokenProvider
{
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken);

    /// <summary>Drop the cached token (e.g. after a 401) so the next call fetches a new one.</summary>
    void Invalidate();
}

/// <summary>
/// OAuth2 client-credentials token source. Singleton: the token is cached until shortly before
/// <c>expires_in</c> and refreshed by one caller at a time.
/// </summary>
public sealed partial class TrackTikAccessTokenProvider : ITrackTikAccessTokenProvider, IDisposable
{
    public const string HttpClientName = "TrackTik.Auth";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<TrackTikOptions> _options;
    private readonly TimeProvider _time;
    private readonly ILogger<TrackTikAccessTokenProvider> _logger;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    private CachedToken? _cached;

    public TrackTikAccessTokenProvider(
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<TrackTikOptions> options,
        TimeProvider time,
        ILogger<TrackTikAccessTokenProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _time = time;
        _logger = logger;
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (TryGetValid(out var token))
        {
            return token;
        }

        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            // Another caller may have refreshed while we waited.
            if (TryGetValid(out token))
            {
                return token;
            }

            _cached = await RequestTokenAsync(cancellationToken);
            return _cached.AccessToken;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public void Invalidate() => _cached = null;

    public void Dispose() => _refreshLock.Dispose();

    private bool TryGetValid(out string token)
    {
        var cached = _cached;
        if (cached is not null && _time.GetUtcNow() < cached.RefreshAt)
        {
            token = cached.AccessToken;
            return true;
        }

        token = string.Empty;
        return false;
    }

    private async Task<CachedToken> RequestTokenAsync(CancellationToken cancellationToken)
    {
        var options = _options.CurrentValue;
        var client = _httpClientFactory.CreateClient(HttpClientName);

        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = options.ClientId,
            ["client_secret"] = options.ClientSecret,
        };
        if (!string.IsNullOrWhiteSpace(options.Scope))
        {
            form["scope"] = options.Scope;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, options.TokenUrl)
        {
            Content = new FormUrlEncodedContent(form),
        };

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new TrackTikApiException("TrackTik token endpoint is unreachable.", null, ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TrackTikApiException("TrackTik token request timed out.", null, ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                // Never log the request body: it contains the client secret.
                throw new TrackTikApiException(
                    $"TrackTik token request failed with HTTP {(int)response.StatusCode}.", response.StatusCode);
            }

            var body = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken);
            if (body is null || string.IsNullOrWhiteSpace(body.AccessToken))
            {
                throw new TrackTikApiException("TrackTik token response did not contain an access_token.", response.StatusCode);
            }

            var lifetime = TimeSpan.FromSeconds(Math.Max(body.ExpiresIn, 0));
            var skew = TimeSpan.FromSeconds(Math.Min(options.TokenRefreshSkewSeconds, lifetime.TotalSeconds / 2));
            var refreshAt = _time.GetUtcNow() + lifetime - skew;

            LogTokenAcquired(body.ExpiresIn);
            return new CachedToken(body.AccessToken, refreshAt);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Acquired TrackTik access token (expires in {ExpiresIn}s)")]
    private partial void LogTokenAcquired(int expiresIn);

    private sealed record CachedToken(string AccessToken, DateTimeOffset RefreshAt);

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string? AccessToken,
        [property: JsonPropertyName("token_type")] string? TokenType,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}
