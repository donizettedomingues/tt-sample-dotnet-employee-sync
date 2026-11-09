using System.Net;

namespace EmployeeSync.Application.Abstractions.TrackTik;

/// <summary>Any failure talking to TrackTik: transport error, timeout, auth failure or non-success status.</summary>
public sealed class TrackTikApiException : Exception
{
    public TrackTikApiException(string message, HttpStatusCode? statusCode = null, Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }

    public HttpStatusCode? StatusCode { get; }
}
