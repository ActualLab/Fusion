namespace ActualLab.CommandR;

/// <summary>
/// Marker interface for all commands processed by <see cref="ICommander"/>.
/// </summary>
public interface ICommand;

/// <summary>
/// A command that produces a result of type <typeparamref name="TResult"/>.
/// </summary>
public interface ICommand<TResult> : ICommand;
