namespace ActualLab.CommandR.Internal;

/// <summary>
/// Defines well-known priority constants for built-in command handlers.
/// </summary>
public static class CommanderCommandHandlerPriority
{
    public const double PreparedCommandHandler = 1_000_000_000;
    public const double CommandTracer = 998_000_000;
    public const double LocalCommandRunner = 900_000_000;
    public const double RpcRoutingCommandHandler = 800_000_000;
}
