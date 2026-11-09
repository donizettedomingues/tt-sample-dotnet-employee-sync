using EmployeeSync.Application.Abstractions.Events;
using EmployeeSync.Domain.Common;

namespace EmployeeSync.UnitTests.Fakes;

public sealed class RecordingEventPublisher : IEventPublisher
{
    public List<IDomainEvent> Published { get; } = [];

    public bool Throw { get; set; }

    public Task PublishAsync(IDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        if (Throw)
        {
            throw new InvalidOperationException("broker down");
        }

        Published.Add(domainEvent);
        return Task.CompletedTask;
    }
}
