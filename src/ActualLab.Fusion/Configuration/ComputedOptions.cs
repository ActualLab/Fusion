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
    public TimeSpan TransientErrorInvalidationDelay { get; init; } = TimeSpan.FromSeconds(1);
    public TimeSpan AutoInvalidationDelay { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; init; }
        = TimeSpan.MaxValue; // No auto invalidation
    public TimeSpan InvalidationDelay { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; init; }
    public RemoteComputedCacheMode RemoteComputedCacheMode { get; init; }
        = RemoteComputedCacheMode.NoCache;
    public ComputedCancellationReprocessingOptions CancellationReprocessing { get; init; }
        = ComputedCancellationReprocessingOptions.Default;

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
        if (a == null)
            return null;

        var defaultOptions = isClientServiceMethod ? ClientDefault : Default;
        // (Auto)InvalidationDelay for replicas should be taken from ReplicaMethodAttribute only
        var autoInvalidationDelay = isClientServiceMethod
            ? rma?.AutoInvalidationDelay ?? double.NaN
            : a.AutoInvalidationDelay;
        var invalidationDelay = isClientServiceMethod
            ? rma?.InvalidationDelay ?? double.NaN
            : a.InvalidationDelay;
        // Default cache behavior must be changed to null to let it "inherit" defaultOptions.ClientCacheMode
        var rmaCacheMode = rma?.CacheMode;
        if (rmaCacheMode == RemoteComputedCacheMode.Default)
            rmaCacheMode = null;

        var options = defaultOptions with {
            MinCacheDuration = ToTimeSpan(a.MinCacheDuration) ?? defaultOptions.MinCacheDuration,
            TransientErrorInvalidationDelay = ToTimeSpan(a.TransientErrorInvalidationDelay) ?? defaultOptions.TransientErrorInvalidationDelay,
            AutoInvalidationDelay = ToTimeSpan(autoInvalidationDelay) ?? defaultOptions.AutoInvalidationDelay,
            InvalidationDelay = ToTimeSpan(invalidationDelay) ?? defaultOptions.InvalidationDelay,
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
