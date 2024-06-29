namespace ActualLab.Fusion.Internal;

public static class ComputedImpl
{
    // TrySetOutput

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TrySetOutput<T>(Computed<T> computed, Result<T> output)
        => computed.TrySetOutput(output);

    // Invalidation

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void InvalidateFromCall(ComputedBase computed)
        => computed.InvalidateFromCall();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void StartAutoInvalidation(ComputedBase computed)
        => computed.StartAutoInvalidation();

    // Keep-alive timeouts

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RenewTimeouts(ComputedBase computed, bool isNew)
        => computed.RenewTimeouts(isNew);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CancelTimeouts(ComputedBase computed)
        => computed.CancelTimeouts();

    // Dependency tracking

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ComputedBase[] GetUsed(ComputedBase computed)
        => computed.GetUsed();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (ComputedInput Input, ulong Version)[] GetUsedBy(ComputedBase computed)
        => computed.GetUsedBy();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AddUsed(ComputedBase computed, ComputedBase used)
        => computed.AddUsed(used);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RemoveUsedBy(ComputedBase computed, ComputedBase usedBy)
        => computed.RemoveUsedBy(usedBy);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (int OldCount, int NewCount) PruneUsedBy(ComputedBase computed)
        => computed.PruneUsedBy();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CopyUsedTo(ComputedBase computed, ref ArrayBuffer<ComputedBase> buffer)
        => computed.CopyUsedTo(ref buffer);

    public static void CopyAllUsedTo(ComputedBase computed, ref ArrayBuffer<ComputedBase> buffer)
    {
        var startCount = buffer.Count;
        computed.CopyUsedTo(ref buffer);
        var endCount = buffer.Count;
        for (var i = startCount; i < endCount; i++) {
            var c = buffer[i];
            c.CopyUsedTo(ref buffer);
        }
    }

    // Error handling

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsTransientError(ComputedBase computed, Exception error)
        => computed.IsTransientError(error);
}
