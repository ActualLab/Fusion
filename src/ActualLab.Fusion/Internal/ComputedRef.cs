namespace ActualLab.Fusion.Internal;

/// <summary>
/// A strongly-typed lightweight reference to a <see cref="Computed{T}"/>
/// that can be resolved from the <see cref="ComputedRegistry"/>.
/// </summary>
[method: MethodImpl(MethodImplOptions.AggressiveInlining)]
public sealed record ComputedRef<T>(ComputedInput Input, ulong Version) : ComputedRef(Input, Version)
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ComputedRef(Computed computed)
        : this(computed.Input, computed.Version)
    { }

    [method: MethodImpl(MethodImplOptions.AggressiveInlining)]
    public new Computed<T>? TryResolve()
        => Input.GetExistingComputed() is { } c && c.Version == Version
            ? (Computed<T>)c
            : null;
}

/// <summary>
/// A lightweight reference to a <see cref="Computed"/> identified by its input and version,
/// resolvable from the <see cref="ComputedRegistry"/>.
/// </summary>
[method: MethodImpl(MethodImplOptions.AggressiveInlining)]
public record ComputedRef(ComputedInput Input, ulong Version)
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ComputedRef(Computed computed)
        : this(computed.Input, computed.Version)
    { }

    [method: MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Computed? TryResolve()
        => Input.GetExistingComputed() is { } c && c.Version == Version
            ? c
            : null;
}
