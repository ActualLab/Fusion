namespace ActualLab.CommandR.Commands;

public interface IPreparedCommand : ICommand
{
    public Task Prepare(CommandContext context, CancellationToken cancellationToken);
}
