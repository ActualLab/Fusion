using ActualLab.Fusion.Internal;

namespace ActualLab.Fusion;

/// <summary>
/// Provides static helpers to check whether invalidation is active
/// and to begin invalidation scopes.
/// </summary>
public static class Invalidation
{
    public static InvalidationTrackingMode TrackingMode { get; set; } = InvalidationTrackingMode.OriginOnly;

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
