namespace ActualLab.CommandR.Commands;

/// <summary>
/// A command that must be prepared before execution.
/// </summary>
public interface IPreparedCommand : ICommand
{
    public Task Prepare(CommandContext context, CancellationToken cancellationToken);
}
