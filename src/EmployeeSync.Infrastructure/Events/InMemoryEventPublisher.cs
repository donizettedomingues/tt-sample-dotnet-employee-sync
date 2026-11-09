using System.Collections.Concurrent;
using EmployeeSync.Application.Abstractions.Events;
using EmployeeSync.Domain.Common;
using Microsoft.Extensions.Logging;

namespace EmployeeSync.Infrastructure.Events;

/// <summary>Default publisher: keeps events in memory and logs them. Handy for local runs and tests.</summary>
public sealed partial class InMemoryEventPublisher : IEventPublisher
{
    private readonly ConcurrentQueue<IDomainEvent> _published = new();
    private readonly ILogger<InMemoryEventPublisher> _logger;

    public InMemoryEventPublisher(ILogger<InMemoryEventPublisher> logger) => _logger = logger;

    public IReadOnlyCollection<IDomainEvent> Published => _published.ToArray();

    public Task PublishAsync(IDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        _published.Enqueue(domainEvent);
        LogPublished(domainEvent.GetType().Name, domainEvent);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Published {EventType} in memory: {Event}")]
    private partial void LogPublished(string eventType, IDomainEvent @event);
}
