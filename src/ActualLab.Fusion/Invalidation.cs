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
        get => (ComputeContext.Current.CallOptions & CallOptions.Invalidate) == CallOptions.Invalidate;
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

    public static DeferInvalidationScopeHandle BeginDeferred(IDeferInvalidationHandler? handler = null)
        => DeferInvalidationScope.Begin(handler);

    public static void Defer(Action action)
        => Defer(() => {
            action.Invoke();
            return Task.CompletedTask;
        });

    public static void Defer(Func<Task> action)
    {
        if (IsActive)
            throw Errors.DeferInvalidationInsideInvalidationPass();

        var scope = DeferInvalidationScope.Current ?? throw Errors.NoDeferInvalidationScope();
        scope.Add(action);
    }
}
