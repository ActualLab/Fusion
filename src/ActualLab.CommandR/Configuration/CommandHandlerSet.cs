namespace ActualLab.CommandR.Configuration;

/// <summary>
/// Holds all resolved <see cref="CommandHandlerChain"/> instances for a specific command type,
/// supporting both regular commands and event commands with multiple chains.
/// </summary>
public sealed class CommandHandlerSet
{
    public Type CommandType { get; }
    public ImmutableDictionary<string, CommandHandlerChain> HandlerChains { get; }
    public CommandHandlerChain SingleHandlerChain { get; }

    public CommandHandlerSet(Type commandType, CommandHandlerChain singleHandlerChain)
    {
        CommandType = commandType;
        SingleHandlerChain = singleHandlerChain;
        HandlerChains = ImmutableDictionary<string, CommandHandlerChain>.Empty;
    }

    public CommandHandlerSet(Type commandType, ImmutableDictionary<string, CommandHandlerChain> handlerChains)
    {
        CommandType = commandType;
        HandlerChains = handlerChains;
        SingleHandlerChain = CommandHandlerChain.Empty;
    }

    public CommandHandlerChain GetHandlerChain(ICommand command)
    {
        if (command is not IEventCommand eventCommand)
            return SingleHandlerChain;

        var chainId = eventCommand.ChainId;
        if (chainId.IsNullOrEmpty())
            return CommandHandlerChain.Empty;

        return HandlerChains.TryGetValue(chainId, out var result)
            ? result
            : CommandHandlerChain.Empty;
    }
}
