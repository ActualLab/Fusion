namespace ActualLab.CommandR.Configuration;

public class FuncCommandHandlerFilter(Func<CommandHandler, Type, bool> filter) : CommandHandlerFilter
{
    public Func<CommandHandler, Type, bool> Filter { get; } = filter;

    public override bool IsCommandHandlerUsed(CommandHandler commandHandler, Type commandType)
        => Filter(commandHandler, commandType);
}
