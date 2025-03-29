namespace ActualLab.Fusion.Internal;

public static partial class ComputedImpl
{
    // TrySetOutput

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TrySetValue(Computed computed, object? output)
        => computed.TrySetOutput(Result.NewUntyped(output));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TrySetError(Computed computed, Exception exception)
        => computed.TrySetOutput(Result.NewUntypedError(exception));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TrySetOutput(Computed computed, Result output)
        => computed.TrySetOutput(output);

    // Invalidation

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void InvalidateFromCall(Computed computed)
        => computed.InvalidateFromCall();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void StartAutoInvalidation(Computed computed)
        => computed.StartAutoInvalidation();

    // Keep-alive timeouts

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RenewTimeouts(Computed computed, bool isNew)
        => computed.RenewTimeouts(isNew);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CancelTimeouts(Computed computed)
        => computed.CancelTimeouts();

    // Dependency tracking

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Computed[] GetDependencies(Computed computed)
        => computed.GetDependencies();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (ComputedInput Input, ulong Version)[] GetDependants(Computed computed)
        => computed.GetDependants();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AddDependency(Computed computed, Computed dependency)
        => computed.AddDependency(dependency);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RemoveDependant(Computed computed, Computed dependant)
        => computed.RemoveDependant(dependant);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (int OldCount, int NewCount) PruneDependants(Computed computed)
        => computed.PruneDependants();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CopyDependenciesTo(Computed computed, ref ArrayBuffer<Computed> buffer)
        => computed.CopyDependenciesTo(ref buffer);

    // Error handling

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsTransientError(Computed computed, Exception error)
        => computed.IsTransientError(error);
}
