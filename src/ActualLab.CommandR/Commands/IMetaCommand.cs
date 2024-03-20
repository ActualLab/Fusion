namespace ActualLab.CommandR.Commands;

// A tagging interface for any command that's triggered
// as a consequence of another command, i.e. for
// "second-order" commands
public interface IMetaCommand : ICommand<Unit>
{
    public ICommand UntypedCommand { get; }
}

public interface IMetaCommand<out TCommand> : IMetaCommand
    where TCommand : class, ICommand
{
    TCommand Command { get; }
}
