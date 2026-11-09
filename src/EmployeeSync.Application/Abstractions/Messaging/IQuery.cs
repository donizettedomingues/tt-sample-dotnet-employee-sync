namespace EmployeeSync.Application.Abstractions.Messaging;

/// <summary>A request that only reads state. Handled by exactly one <see cref="IQueryHandler{TQuery,TResult}"/>.</summary>
public interface IQuery<TResult>;

public interface IQueryHandler<in TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    Task<TResult> HandleAsync(TQuery query, CancellationToken cancellationToken);
}
