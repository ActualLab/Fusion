using ActualLab.Fusion.Internal;

namespace ActualLab.Fusion;

public static class Invalidation
{
    public static InvalidationTrackingMode TrackingMode { get; set; } = InvalidationTrackingMode.WholeChain;

    public static bool IsActive
        => (ComputeContext.Current.CallOptions & CallOptions.Invalidate) == CallOptions.Invalidate;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ComputeContextScope Begin(InvalidationSource source)
        => new(new ComputeContext(source));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ComputeContextScope Begin(
        [CallerFilePath] string? file = null,
        [CallerMemberName] string? member = null,
        [CallerLineNumber] int line = 0)
        => new(new ComputeContext(new InvalidationSource(file, member, line)));
}
