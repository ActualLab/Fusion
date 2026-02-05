namespace ActualLab.CommandR.Configuration;

/// <summary>
/// Defines the contract for filtering which command handlers are used for a given command type.
/// </summary>
public abstract class CommandHandlerFilter
{
    public abstract bool IsCommandHandlerUsed(CommandHandler commandHandler, Type commandType);
}
