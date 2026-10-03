using ActualLab.Fusion.Internal;

namespace ActualLab.Fusion;

/// <summary>
/// Provides static helpers to check whether invalidation is active
/// and to begin invalidation scopes.
/// </summary>
public static class Invalidation
{
    public static InvalidationTrackingMode TrackingMode { get; set; } = InvalidationTrackingMode.OriginOnly;

    public static bool IsActive {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ComputeContext.Current.CallOptions.HasFlag(CallOptions.Invalidate);
    }

    // True while a Replicated or Distributed operation's blocks run, i.e. while their calls are
    // recorded rather than applied. It isn't IsActive - no CallOptions.Invalidate - but the same
    // things are forbidden inside it, so whatever rejects an invalidation pass must reject this too.
    public static bool IsCapturing {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ComputeContext.Current.CallOptions.HasFlag(CallOptions.CaptureInvalidation);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ComputeContextScope Begin(InvalidationSource source)
        => new(new ComputeContext(source));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ComputeContextScope Begin(
        [CallerFilePath] string? file = null,
        [CallerMemberName] string? member = null,
        [CallerLineNumber] int line = 0)
        => new(new ComputeContext(new InvalidationSource(file, member, line)));

    // Deferred invalidation

    public static void Defer(Action block)
    {
        if (IsActive || IsCapturing)
            throw Errors.DeferredInvalidationInsideInvalidationPass();

        var context = DeferredInvalidationContext.Current
            ?? throw Errors.NoDeferredInvalidationContext();
        context.AddBlock(block);
    }

    public static void Defer(Func<Task> block)
    {
        if (IsActive || IsCapturing)
            throw Errors.DeferredInvalidationInsideInvalidationPass();

        var context = DeferredInvalidationContext.Current
            ?? throw Errors.NoDeferredInvalidationContext();
        context.AddBlock(block);
    }
}
