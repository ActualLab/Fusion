using ActualLab.CommandR.Internal;

namespace ActualLab.CommandR.Commands;

/// <summary>
/// A command that executes locally.
/// </summary>
public interface ILocalCommand : ICommand
{
    public Task Run(CommandContext context, CancellationToken cancellationToken);
}

/// <summary>
/// A generic variant of <see cref="ILocalCommand"/> that produces a typed result.
/// </summary>
public interface ILocalCommand<T> : ICommand<T>, ILocalCommand;

/// <summary>
/// Base record for local commands that execute inline via a delegate.
/// </summary>
public abstract record LocalCommand : ILocalCommand
{
    public string Title { get; init; } = "";

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static LocalActionCommand New(Func<CommandContext, CancellationToken, Task> handler)
        => new(handler);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static LocalActionCommand New(Func<CancellationToken, Task> handler)
        => new(handler);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static LocalActionCommand New(Func<Task> handler)
        => new(handler);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static LocalActionCommand New(Action<CommandContext, CancellationToken> handler)
        => new(handler);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static LocalActionCommand New(Action<CancellationToken> handler)
        => new(handler);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static LocalActionCommand New(Action handler)
        => new(handler);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static LocalFuncCommand<T> New<T>(Func<CommandContext, CancellationToken, Task<T>> handler)
        => new(handler);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static LocalFuncCommand<T> New<T>(Func<CancellationToken, Task<T>> handler)
        => new(handler);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static LocalFuncCommand<T> New<T>(Func<Task<T>> handler)
        => new(handler);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static LocalFuncCommand<T> New<T>(Func<CommandContext, CancellationToken, T> handler)
        => new(handler);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static LocalFuncCommand<T> New<T>(Func<CancellationToken, T> handler)
        => new(handler);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static LocalFuncCommand<T> New<T>(Func<T> handler)
        => new(handler);

    public abstract Task Run(CommandContext context, CancellationToken cancellationToken);
}
