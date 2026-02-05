namespace ActualLab.Fusion;

/// <summary>
/// Defines a command that is associated with a <see cref="Session"/>.
/// </summary>
public interface ISessionCommand : ICommand
{
    public Session Session { get; init; }
}

/// <summary>
/// A strongly-typed <see cref="ISessionCommand"/> that returns a result of type <typeparamref name="TResult"/>.
/// </summary>
/// <typeparam name="TResult">The type of the command result.</typeparam>
public interface ISessionCommand<TResult> : ICommand<TResult>, ISessionCommand;
