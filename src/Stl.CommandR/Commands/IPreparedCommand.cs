namespace ActualLab.CommandR.Commands;

public interface IPreparedCommand : ICommand
{
    Task Prepare(CommandContext context, CancellationToken cancellationToken);
}
