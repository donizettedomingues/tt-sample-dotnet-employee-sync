using EmployeeSync.Domain.Common;

namespace EmployeeSync.Application.Abstractions.Events;

/// <summary>Publishes domain events to the outside world (in-memory or NATS, chosen by configuration).</summary>
public interface IEventPublisher
{
    Task PublishAsync(IDomainEvent domainEvent, CancellationToken cancellationToken);
}
