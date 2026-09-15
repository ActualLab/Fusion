namespace ActualLab.Async;

/// <summary>
/// Extension methods and helpers for <see cref="ExecutionContext"/>.
/// </summary>
public static class ExecutionContextExt
{
    private const string DefaultFieldName
#if NETSTANDARD2_0 || NETSTANDARD2_1
        = "s_dummyDefaultEC";
#else
        = "Default";
#endif
    [ThreadStatic] private static Func<Task>? _taskFactory0;
    [ThreadStatic] private static Func<object?, Task>? _taskFactory1;
    [ThreadStatic] private static Task? _task;

    public static readonly ExecutionContext Default
#if USE_UNSAFE_ACCESSORS
        = DefaultGetter(null!);

    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = DefaultFieldName)]
    private static extern ref ExecutionContext DefaultGetter(ExecutionContext @this);
#else
        = (ExecutionContext)typeof(ExecutionContext)
            .GetField(DefaultFieldName, BindingFlags.Static | BindingFlags.NonPublic)!
            .GetValue(null)!;
#endif

    public static bool IsDefault {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ReferenceEquals(ExecutionContext.Capture(), Default);
    }

#if NET8_0_OR_GREATER
    public static AsyncFlowControl TrySuppressFlow()
        => ExecutionContext.IsFlowSuppressed()
            ? default
            : ExecutionContext.SuppressFlow();
#else
    public static ClosedDisposable<AsyncFlowControl> TrySuppressFlow()
    {
        if (ExecutionContext.IsFlowSuppressed())
            return default;

        var releaser = ExecutionContext.SuppressFlow();
        return Disposable.NewClosed(releaser, r => r.Dispose());
    }
#endif

    // RunWithDefaultExecutionContext & StartWithDefaultExecutionContext

    // Runs the callback with no AsyncLocals. Unlike ExecutionContext.SuppressFlow(), the context left
    // for Capture() is empty rather than absent - and it's that non-null context which makes a resumed
    // continuation restore its thread's SynchronizationContext. Without it, any inline-completed
    // continuation can strand one: that's how Blazor's dispatcher leaks the renderer's to a pool thread.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RunWithDefaultExecutionContext(ContextCallback callback, object? state = null)
    {
        if (IsDefault)
            callback.Invoke(state);
        else
            ExecutionContext.Run(Default, callback, state);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task StartWithDefaultExecutionContext(Func<Task> taskFactory)
        => IsDefault
            ? taskFactory.Invoke()
            : Start(Default, taskFactory);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task StartWithDefaultExecutionContext(Func<object?, Task> taskFactory, object? state = null)
        => IsDefault
            ? taskFactory.Invoke(state)
            : Start(Default, taskFactory, state);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<T> StartWithDefaultExecutionContext<T>(Func<Task<T>> taskFactory)
        => IsDefault
            ? taskFactory.Invoke()
            : Start(Default, taskFactory);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<T> StartWithDefaultExecutionContext<T>(Func<object?, Task<T>> taskFactory, object? state = null)
        => IsDefault
            ? taskFactory.Invoke(state)
            : Start(Default, taskFactory, state);

    // Start

    public static Task Start(
        ExecutionContext executionContext,
        Func<Task> taskFactory)
    {
        var oldTask = _task;
        try {
            _taskFactory0 = taskFactory;
            ExecutionContext.Run(executionContext, static _ => _task = _taskFactory0.Invoke(), state: null);
            return _task!;
        }
        finally {
            _task = oldTask;
        }
    }

    public static Task Start(
        ExecutionContext executionContext,
        Func<object?, Task> taskFactory,
        object? state = null)
    {
        var oldTask = _task;
        try {
            _taskFactory1 = taskFactory;
            ExecutionContext.Run(executionContext, static state => _task = _taskFactory1.Invoke(state), state);
            return _task!;
        }
        finally {
            _task = oldTask;
        }
    }

    public static Task<T> Start<T>(
        ExecutionContext executionContext,
        Func<Task<T>> taskFactory)
    {
        var oldTask = _task;
        try {
            _taskFactory0 = taskFactory;
            ExecutionContext.Run(executionContext, static _ => _task = _taskFactory0.Invoke(), state: null);
            return (Task<T>)_task!;
        }
        finally {
            _task = oldTask;
        }
    }

    public static Task<T> Start<T>(
        ExecutionContext executionContext,
        Func<object?, Task<T>> taskFactory,
        object? state = null)
    {
        var oldTask = _task;
        try {
            _taskFactory1 = taskFactory;
            ExecutionContext.Run(executionContext, static state => _task = _taskFactory1.Invoke(state), state);
            return (Task<T>)_task!;
        }
        finally {
            _task = oldTask;
        }
    }
}
