namespace EmployeeSync.Application.Abstractions.Messaging;

/// <summary>A request that changes state. Handled by exactly one <see cref="ICommandHandler{TCommand,TResult}"/>.</summary>
public interface ICommand<TResult>;

public interface ICommandHandler<in TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    Task<TResult> HandleAsync(TCommand command, CancellationToken cancellationToken);
}
