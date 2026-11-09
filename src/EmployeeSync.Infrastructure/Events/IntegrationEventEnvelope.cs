using EmployeeSync.Domain.Common;
using EmployeeSync.Domain.Employees;

namespace EmployeeSync.Infrastructure.Events;

/// <summary>Wire format for published events, independent of the domain type's shape.</summary>
public sealed record IntegrationEventEnvelope(Guid Id, string Type, DateTimeOffset OccurredAt, object Data)
{
    public static IntegrationEventEnvelope From(IDomainEvent domainEvent) =>
        new(Guid.NewGuid(), EventName(domainEvent), domainEvent.OccurredAt, domainEvent);

    /// <summary>Stable event name, also used as the NATS subject suffix.</summary>
    public static string EventName(IDomainEvent domainEvent) => domainEvent switch
    {
        EmployeeSynced => "synced",
        _ => domainEvent.GetType().Name.ToLowerInvariant(),
    };
}
