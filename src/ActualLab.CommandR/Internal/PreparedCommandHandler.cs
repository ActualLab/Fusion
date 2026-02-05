namespace ActualLab.CommandR.Internal;

/// <summary>
/// Built-in command handler that invokes <see cref="IPreparedCommand.Prepare"/>
/// before continuing the handler pipeline.
/// </summary>
public sealed class PreparedCommandHandler : ICommandHandler<IPreparedCommand>
{
    [CommandFilter(Priority = CommanderCommandHandlerPriority.PreparedCommandHandler)]
    public async Task OnCommand(IPreparedCommand command, CommandContext context, CancellationToken cancellationToken)
    {
        await command.Prepare(context, cancellationToken).ConfigureAwait(false);
        await context.InvokeRemainingHandlers(cancellationToken).ConfigureAwait(false);
    }
}
