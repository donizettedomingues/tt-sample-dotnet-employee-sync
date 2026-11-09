using System.Text.Json;
using EmployeeSync.Application.Abstractions.Events;
using EmployeeSync.Domain.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NATS.Client.Core;

namespace EmployeeSync.Infrastructure.Events;

/// <summary>
/// Publishes events as JSON to NATS core on "{SubjectPrefix}.{event}", e.g. <c>tracktik.employees.synced</c>.
/// Enabled with Events__Publisher=Nats. Core NATS is fire-and-forget; JetStream would add persistence.
/// </summary>
public sealed partial class NatsEventPublisher : IEventPublisher, IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly NatsConnection _connection;
    private readonly string _subjectPrefix;
    private readonly ILogger<NatsEventPublisher> _logger;

    public NatsEventPublisher(IOptions<EventsOptions> options, ILogger<NatsEventPublisher> logger)
    {
        var nats = options.Value.Nats;
        _connection = new NatsConnection(new NatsOpts { Url = nats.Url, Name = "employee-sync-api" });
        _subjectPrefix = nats.SubjectPrefix;
        _logger = logger;
    }

    public async Task PublishAsync(IDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        var envelope = IntegrationEventEnvelope.From(domainEvent);
        var subject = $"{_subjectPrefix}.{envelope.Type}";
        var payload = JsonSerializer.SerializeToUtf8Bytes(envelope, envelope.GetType(), JsonOptions);

        await _connection.PublishAsync(subject, payload, cancellationToken: cancellationToken);
        LogPublished(subject, envelope.Id);
    }

    public ValueTask DisposeAsync() => _connection.DisposeAsync();

    [LoggerMessage(Level = LogLevel.Information, Message = "Published event {EventId} to NATS subject {Subject}")]
    private partial void LogPublished(string subject, Guid eventId);
}
