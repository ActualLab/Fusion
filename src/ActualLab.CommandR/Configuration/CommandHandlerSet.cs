namespace ActualLab.CommandR.Configuration;

public sealed record CommandHandlerSet
{
    public Type CommandType { get; }
    public ImmutableDictionary<Symbol, CommandHandlerChain> HandlerChains { get; }
    public CommandHandlerChain SingleHandlerChain { get; }

    public CommandHandlerSet(Type commandType, CommandHandlerChain singleHandlerChain)
    {
        CommandType = commandType;
        SingleHandlerChain = singleHandlerChain;
        HandlerChains = ImmutableDictionary<Symbol, CommandHandlerChain>.Empty;
    }

    public CommandHandlerSet(Type commandType, ImmutableDictionary<Symbol, CommandHandlerChain> handlerChains)
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
        if (chainId.IsEmpty)
            return CommandHandlerChain.Empty;

        return HandlerChains.TryGetValue(chainId, out var result)
            ? result
            : CommandHandlerChain.Empty;
    }

    // This record relies on reference-based equality
    public bool Equals(CommandHandlerSet? other) => ReferenceEquals(this, other);
    public override int GetHashCode() => RuntimeHelpers.GetHashCode(this);
}
