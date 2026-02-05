namespace ActualLab.Async;

/// <summary>
/// Factory methods for creating lightweight <see cref="IAsyncDisposable"/> instances.
/// </summary>
public static class AsyncDisposable
{
    public static AsyncDisposable<Func<ValueTask>> New(Func<ValueTask> disposeHandler)
        => new(static func => func.Invoke(), disposeHandler);

    public static AsyncDisposable<TState> New<TState>(Func<TState, ValueTask> disposeHandler, TState state)
        => new(disposeHandler, state);
}

/// <summary>
/// A lightweight async disposable struct that invokes a handler with captured state on disposal.
/// </summary>
public readonly struct AsyncDisposable<TState>(Func<TState, ValueTask>? disposeHandler, TState state)
    : IAsyncDisposable
{
    public ValueTask DisposeAsync()
        => disposeHandler?.Invoke(state) ?? default;
}
