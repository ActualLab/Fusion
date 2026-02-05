namespace ActualLab.CommandR.Configuration;

/// <summary>
/// Extension methods for <see cref="CommandHandlerResolver"/>.
/// </summary>
[UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "We assume all command handling code is preserved")]
public static class CommandHandlerResolverExt
{
    public static CommandHandlerSet GetCommandHandlers(this CommandHandlerResolver resolver, ICommand command)
        => resolver.GetCommandHandlers(command.GetType());

    public static CommandHandlerChain GetCommandHandlerChain(this CommandHandlerResolver resolver, ICommand command)
        => resolver.GetCommandHandlers(command.GetType()).GetHandlerChain(command);
}
