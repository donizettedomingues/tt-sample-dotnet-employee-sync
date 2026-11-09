namespace EmployeeSync.Domain.Common;

/// <summary>
/// Base class for aggregates that collect domain events. Events are only collected here;
/// the application layer dispatches them after the state change has been committed.
/// </summary>
public abstract class AggregateRoot
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}
