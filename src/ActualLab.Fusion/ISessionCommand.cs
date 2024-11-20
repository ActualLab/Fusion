namespace ActualLab.Fusion;

public interface ISessionCommand : ICommand
{
    public Session Session { get; init; }
}

public interface ISessionCommand<TResult> : ICommand<TResult>, ISessionCommand;
