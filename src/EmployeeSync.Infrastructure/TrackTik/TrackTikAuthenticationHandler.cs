using System.Net;
using System.Net.Http.Headers;

namespace EmployeeSync.Infrastructure.TrackTik;

/// <summary>
/// Adds the Bearer token to every TrackTik API call. On a 401 it drops the cached token and
/// retries once, which covers a token revoked or expired earlier than advertised.
/// </summary>
public sealed class TrackTikAuthenticationHandler : DelegatingHandler
{
    private readonly ITrackTikAccessTokenProvider _tokens;

    public TrackTikAuthenticationHandler(ITrackTikAccessTokenProvider tokens) => _tokens = tokens;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await _tokens.GetAccessTokenAsync(cancellationToken));
        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }

        response.Dispose();
        _tokens.Invalidate();
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await _tokens.GetAccessTokenAsync(cancellationToken));
        return await base.SendAsync(request, cancellationToken);
    }
}
