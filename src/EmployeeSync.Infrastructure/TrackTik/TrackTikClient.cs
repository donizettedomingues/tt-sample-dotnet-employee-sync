using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using EmployeeSync.Application.Abstractions.TrackTik;

namespace EmployeeSync.Infrastructure.TrackTik;

/// <summary>
/// Typed HttpClient for the TrackTik Employees API. BaseAddress is the API base URL (incl. /v1);
/// authentication is added by <see cref="TrackTikAuthenticationHandler"/>.
/// </summary>
public sealed class TrackTikClient : ITrackTikClient
{
    public const string HttpClientName = "TrackTik.Api";
    private const int MaxErrorDetailLength = 300;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _http;

    public TrackTikClient(HttpClient http) => _http = http;

    public async Task<TrackTikEmployeeResponse> CreateEmployeeAsync(TrackTikEmployee employee, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Post, "employees", employee, cancellationToken);
        await EnsureSuccessAsync(response, "POST /employees", cancellationToken);
        return await ReadEmployeeAsync(response, employee.EmployeeId, cancellationToken);
    }

    public async Task<TrackTikEmployeeResponse?> UpdateEmployeeAsync(string employeeId, TrackTikEmployee employee, CancellationToken cancellationToken)
    {
        var path = $"employees/{Uri.EscapeDataString(employeeId)}";
        using var response = await SendAsync(HttpMethod.Put, path, employee, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessAsync(response, "PUT /employees/{employeeId}", cancellationToken);
        return await ReadEmployeeAsync(response, employeeId, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, TrackTikEmployee body, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, path)
        {
            Content = JsonContent.Create(body, options: JsonOptions),
        };

        try
        {
            return await _http.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new TrackTikApiException($"TrackTik is unreachable: {ex.Message}", null, ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TrackTikApiException("TrackTik request timed out.", null, ex);
        }
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, string operation, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var detail = await ReadErrorMessageAsync(response, cancellationToken);
        throw new TrackTikApiException(
            $"TrackTik {operation} returned HTTP {(int)response.StatusCode}{(detail is null ? string.Empty : $": {detail}")}",
            response.StatusCode);
    }

    private static async Task<TrackTikEmployeeResponse> ReadEmployeeAsync(HttpResponseMessage response, string fallbackEmployeeId, CancellationToken cancellationToken)
    {
        try
        {
            var envelope = await response.Content.ReadFromJsonAsync<Envelope>(JsonOptions, cancellationToken);
            return new TrackTikEmployeeResponse(envelope?.Data?.Id, envelope?.Data?.EmployeeId ?? fallbackEmployeeId);
        }
        catch (JsonException)
        {
            // A 2xx with an unexpected body still means TrackTik accepted the employee.
            return new TrackTikEmployeeResponse(null, fallbackEmployeeId);
        }
    }

    /// <summary>Extracts error.message from TrackTik's error envelope, falling back to a truncated raw body.</summary>
    private static async Task<string?> ReadErrorMessageAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        try
        {
            var envelope = JsonSerializer.Deserialize<Envelope>(raw, JsonOptions);
            if (!string.IsNullOrWhiteSpace(envelope?.Error?.Message))
            {
                return envelope.Error.Message;
            }
        }
        catch (JsonException)
        {
            // Not JSON; use the raw text below.
        }

        return raw.Length > MaxErrorDetailLength ? raw[..MaxErrorDetailLength] : raw;
    }

    private sealed record Envelope(bool Success, EnvelopeData? Data, EnvelopeError? Error);

    private sealed record EnvelopeData(string? Id, string? EmployeeId);

    private sealed record EnvelopeError(string? Code, string? Message);
}
