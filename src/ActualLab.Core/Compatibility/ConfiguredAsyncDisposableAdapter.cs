namespace ActualLab.Compatibility;

/// <summary>
/// Wraps an <see cref="AsyncDisposableAdapter{T}"/> with a configured-await flag
/// for cross-framework async disposal.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly struct ConfiguredAsyncDisposableAdapter<T>
#if !NETSTANDARD2_0
    where T : IAsyncDisposable?
#else
    where T : IDisposable?
#endif
{
    public T Target { get; }
    private readonly bool _continueOnCapturedContext;

    internal ConfiguredAsyncDisposableAdapter(AsyncDisposableAdapter<T> source, bool continueOnCapturedContext)
    {
        Target = source.Target;
        _continueOnCapturedContext = continueOnCapturedContext;
    }

    public ConfiguredValueTaskAwaitable DisposeAsync()
    {
#if !NETSTANDARD2_0
        return Target?.DisposeAsync().ConfigureAwait(_continueOnCapturedContext)
            ?? default(ValueTask).ConfigureAwait(_continueOnCapturedContext);
#else
        if (Target is IAsyncDisposable ad)
            return ad.DisposeAsync().ConfigureAwait(_continueOnCapturedContext);
        Target?.Dispose();
        return default(ValueTask).ConfigureAwait(_continueOnCapturedContext);
#endif
    }
}
