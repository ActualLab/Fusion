namespace ActualLab.CommandR.Configuration;

public abstract class CommandHandlerFilter
{
    public abstract bool IsCommandHandlerUsed(CommandHandler commandHandler, Type commandType);
}
