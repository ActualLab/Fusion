namespace ActualLab.Fusion;

public static class ComputedSourceExt
{
    // Computed-like methods

    public static ValueTask Update(this IComputedSource computedSource,
        CancellationToken cancellationToken = default)
    {
        var valueTask = computedSource.Computed.UpdateUntyped(cancellationToken);
        if (!valueTask.IsCompleted)
            return new ValueTask(valueTask.AsTask());

        _ = valueTask.Result;
        return default;
    }

    public static Task<T> Use<T>(this ComputedSource<T> computedSource,
        CancellationToken cancellationToken = default)
        => (Task<T>)computedSource.Computed.UseUntyped(allowInconsistent: false, cancellationToken);

    public static Task<T> Use<T>(this ComputedSource<T> computedSource,
        bool allowInconsistent, CancellationToken cancellationToken = default)
        => (Task<T>)computedSource.Computed.UseUntyped(allowInconsistent, cancellationToken);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Invalidate(this ComputedSource computedSource,
        bool immediately = false,
        [CallerFilePath] string? file = null,
        [CallerMemberName] string? member = null,
        [CallerLineNumber] int line = 0)
        => computedSource.Computed.Invalidate(immediately, new InvalidationSource(file, member, line));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Invalidate(this ComputedSource computedSource,
        bool immediately, InvalidationSource source)
        => computedSource.Computed.Invalidate(immediately, source);

    public static ValueTask Recompute(this IComputedSource source, CancellationToken cancellationToken = default)
    {
        source.Computed.Invalidate(immediately: true, InvalidationSource.ComputedSourceExtRecompute);
        return source.Update(cancellationToken);
    }
}
