namespace EmployeeSync.Domain.Common;

/// <summary>Marker for something that happened in the domain and that other parts of the system may react to.</summary>
public interface IDomainEvent
{
    DateTimeOffset OccurredAt { get; }
}
