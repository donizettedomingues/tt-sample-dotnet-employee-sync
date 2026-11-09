using System.ComponentModel.DataAnnotations;

namespace EmployeeSync.Infrastructure.TrackTik;

/// <summary>
/// Bound from the "TrackTik" section, i.e. env vars TRACKTIK__TOKENURL, TRACKTIK__APIBASEURL,
/// TRACKTIK__CLIENTID, TRACKTIK__CLIENTSECRET. No values are committed to appsettings.
/// </summary>
public sealed class TrackTikOptions
{
    public const string SectionName = "TrackTik";

    /// <summary>OAuth2 token endpoint (not under /v1).</summary>
    [Required, Url]
    public string TokenUrl { get; set; } = string.Empty;

    /// <summary>API base URL including /v1, e.g. https://.../v1.</summary>
    [Required, Url]
    public string ApiBaseUrl { get; set; } = string.Empty;

    [Required]
    public string ClientId { get; set; } = string.Empty;

    [Required]
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>Optional. When empty the token gets every scope registered for the client.</summary>
    public string? Scope { get; set; }

    [Range(1, 120)]
    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>Refresh the token this many seconds before it actually expires.</summary>
    [Range(0, 600)]
    public int TokenRefreshSkewSeconds { get; set; } = 60;
}
