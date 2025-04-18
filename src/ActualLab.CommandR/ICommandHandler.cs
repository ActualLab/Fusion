namespace ActualLab.CommandR;

public interface ICommandHandler;

public interface ICommandHandler<in TCommand> : ICommandHandler
    where TCommand : class, ICommand
{
    public Task OnCommand(
        TCommand command, CommandContext context,
        CancellationToken cancellationToken);
}
