namespace ActualLab.CommandR;

/// <summary>
/// Marker interface for all command handlers.
/// </summary>
public interface ICommandHandler;

/// <summary>
/// Defines a handler that processes commands of type <typeparamref name="TCommand"/>.
/// </summary>
public interface ICommandHandler<in TCommand> : ICommandHandler
    where TCommand : class, ICommand
{
    public Task OnCommand(
        TCommand command, CommandContext context,
        CancellationToken cancellationToken);
}
