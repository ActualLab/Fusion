namespace ActualLab.CommandR.Internal;

/// <summary>
/// Built-in command handler that executes <see cref="ILocalCommand"/> instances.
/// </summary>
public sealed class LocalCommandRunner : ICommandHandler<ILocalCommand>
{
    [CommandHandler(Priority = CommanderCommandHandlerPriority.LocalCommandRunner)]
    public Task OnCommand(ILocalCommand command, CommandContext context, CancellationToken cancellationToken)
        => command.Run(context, cancellationToken);
}
