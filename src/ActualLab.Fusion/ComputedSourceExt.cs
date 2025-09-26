namespace ActualLab.Fusion;

public static class ComputedSourceExt
{
    // Computed-like methods

    public static ValueTask Update(this IComputedSource source, CancellationToken cancellationToken = default)
    {
        var valueTask = source.Computed.UpdateUntyped(cancellationToken);
        if (!valueTask.IsCompleted)
            return new ValueTask(valueTask.AsTask());

        _ = valueTask.Result;
        return default;
    }

    public static Task<T> Use<T>(this ComputedSource<T> source, CancellationToken cancellationToken = default)
        => (Task<T>)source.Computed.UseUntyped(allowInconsistent: false, cancellationToken);

    public static Task<T> Use<T>(this ComputedSource<T> source, bool allowInconsistent, CancellationToken cancellationToken = default)
        => (Task<T>)source.Computed.UseUntyped(allowInconsistent, cancellationToken);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Invalidate(this IComputedSource source, bool immediately = false)
        => source.Computed.Invalidate(immediately);

    public static ValueTask Recompute(this IComputedSource source, CancellationToken cancellationToken = default)
    {
        source.Computed.Invalidate(true);
        return source.Update(cancellationToken);
    }
}
