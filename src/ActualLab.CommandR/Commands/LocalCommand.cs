using ActualLab.CommandR.Internal;

namespace ActualLab.CommandR.Commands;

public interface ILocalCommand : ICommand
{
    public Task Run(CommandContext context, CancellationToken cancellationToken);
}

public interface ILocalCommand<T> : ICommand<T>, ILocalCommand;

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
