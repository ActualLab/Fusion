using System.Diagnostics.CodeAnalysis;
using ActualLab.Interception.Internal;

namespace ActualLab.Fusion;

public record ComputedOptions
{
    public static ComputedOptions Default { get; set; } = new();
    public static ComputedOptions ClientDefault { get; set; } = new() {
        MinCacheDuration = TimeSpan.FromMinutes(1),
        RemoteComputedCacheMode = RemoteComputedCacheMode.Cache,
        CancellationReprocessing = ComputedCancellationReprocessingOptions.ClientDefault,
    };
    public static ComputedOptions MutableStateDefault { get; set; } = new() {
        TransientErrorInvalidationDelay = TimeSpan.MaxValue,
    };

    public TimeSpan MinCacheDuration { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; init; }
        = default; // No min. cache duration = don't add newly produced instances to Timeouts.KeepAlive
    public TimeSpan TransientErrorInvalidationDelay { get; init; }
        = TimeSpan.FromSeconds(1); // Should be positive
    public TimeSpan AutoInvalidationDelay { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; init; }
        = TimeSpan.MaxValue; // No auto invalidation
    public TimeSpan InvalidationDelay { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; init; }
        = default; // No invalidation delay
    public TimeSpan ConsolidationDelay { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; init; }
        = TimeSpan.MaxValue; // No consolidation
    public RemoteComputedCacheMode RemoteComputedCacheMode { get; init; }
        = RemoteComputedCacheMode.NoCache;
    public ComputedCancellationReprocessingOptions CancellationReprocessing { get; init; }
        = ComputedCancellationReprocessingOptions.Default;

    public bool HasMinCacheDuration => MinCacheDuration != TimeSpan.Zero;
    public bool HasInvalidationDelay => InvalidationDelay != TimeSpan.Zero;
    public bool HasTransientErrorInvalidationDelay => TransientErrorInvalidationDelay != TimeSpan.Zero;
    public bool IsAutoInvalidating => AutoInvalidationDelay != TimeSpan.MaxValue;
    public bool IsConsolidating => ConsolidationDelay != TimeSpan.MaxValue;

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "We assume attributes on compute methods are fully preserved")]
    public static ComputedOptions? Get(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type type,
        MethodInfo method)
    {
        var isClientServiceMethod = type.IsInterface || typeof(InterfaceProxy).IsAssignableFrom(type);
        var cma = method.GetAttribute<ComputeMethodAttribute>(true, true);
        var rma = isClientServiceMethod
            ? method.GetAttribute<RemoteComputeMethodAttribute>(true, true)
            : null;
        var a = rma ?? cma;
        if (a is null)
            return null;

        var defaultOptions = isClientServiceMethod ? ClientDefault : Default;
        // (Auto)InvalidationDelay for replicas should be taken from ReplicaMethodAttribute only
        var autoInvalidationDelay = isClientServiceMethod
            ? rma?.AutoInvalidationDelay ?? double.NaN
            : a.AutoInvalidationDelay;
        var invalidationDelay = isClientServiceMethod
            ? rma?.InvalidationDelay ?? double.NaN
            : a.InvalidationDelay;
        if (rma is not null && rma.ConsolidationDelay is not double.NaN)
            throw new InvalidOperationException(
                $"{nameof(ConsolidationDelay)} is unsupported in {nameof(RemoteComputeMethodAttribute)}.");
        var consolidationDelay = a.ConsolidationDelay;

        // Default cache behavior must be changed to null to let it "inherit" defaultOptions.ClientCacheMode
        var rmaCacheMode = rma?.CacheMode;
        if (rmaCacheMode == RemoteComputedCacheMode.Default)
            rmaCacheMode = null;

        var options = defaultOptions with {
            MinCacheDuration = ToTimeSpan(a.MinCacheDuration) ?? defaultOptions.MinCacheDuration,
            TransientErrorInvalidationDelay = ToTimeSpan(a.TransientErrorInvalidationDelay) ?? defaultOptions.TransientErrorInvalidationDelay,
            AutoInvalidationDelay = ToTimeSpan(autoInvalidationDelay) ?? defaultOptions.AutoInvalidationDelay,
            InvalidationDelay = ToTimeSpan(invalidationDelay) ?? defaultOptions.InvalidationDelay,
            ConsolidationDelay = ToTimeSpan(consolidationDelay) ?? defaultOptions.ConsolidationDelay,
            RemoteComputedCacheMode = rmaCacheMode ?? defaultOptions.RemoteComputedCacheMode,
        };
        // We don't want to multiply instances of ComputedOptions here unless they differ from the default ones
        return options == defaultOptions ? defaultOptions : options;
    }

    // Private methods

    private static TimeSpan? ToTimeSpan(double value)
    {
        if (double.IsNaN(value))
            return null;
        if (value >= TimeSpanExt.InfiniteInSeconds)
            return TimeSpan.MaxValue;
        if (value < 0)
            throw new ArgumentOutOfRangeException(nameof(value));
        return TimeSpan.FromSeconds(value);
    }
}
