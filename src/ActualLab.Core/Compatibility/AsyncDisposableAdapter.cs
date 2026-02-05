namespace ActualLab.Compatibility;

/// <summary>
/// Adapts a disposable or async-disposable object into <see cref="IAsyncDisposable"/>
/// across target frameworks.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly struct AsyncDisposableAdapter<T>(T target) : IAsyncDisposable
#if !NETSTANDARD2_0
    where T : IAsyncDisposable?
#else
    where T : IDisposable?
#endif
{
    public T Target { get; } = target;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask DisposeAsync()
    {
#if !NETSTANDARD2_0
        return Target?.DisposeAsync() ?? default;
#else
        if (Target is IAsyncDisposable ad)
            return ad.DisposeAsync();

        Target?.Dispose();
        return default;
#endif
    }

    public ConfiguredAsyncDisposableAdapter<T> ConfigureAwait(bool continueOnCapturedContext)
        => new(this, continueOnCapturedContext);
}
