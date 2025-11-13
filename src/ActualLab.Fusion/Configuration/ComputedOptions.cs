using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using ActualLab.Interception.Internal;

namespace ActualLab.Fusion;

public record ComputedOptions
{
    private static readonly ConcurrentDictionary<(Type, MethodInfo), ComputedOptions?> Cache = new();

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
        => Cache.GetOrAdd((type, method), static key => {
            var (type, method) = key;
            var isRemoteServiceMethod = type.IsInterface || typeof(InterfaceProxy).IsAssignableFrom(type);
            var cma = method.GetAttribute<ComputeMethodAttribute>(inheritFromInterfaces: true, inheritFromBaseTypes: true);
            var rma = isRemoteServiceMethod
                ? method.GetAttribute<RemoteComputeMethodAttribute>(inheritFromInterfaces: true, inheritFromBaseTypes: true)
                : null;
            var a = rma ?? cma;
            if (a is null)
                return null;

            var defaultOptions = isRemoteServiceMethod ? ClientDefault : Default;
            // (Auto)InvalidationDelay for replicas should be taken from ReplicaMethodAttribute only
            var autoInvalidationDelay = isRemoteServiceMethod
                ? rma?.AutoInvalidationDelay ?? double.NaN
                : a.AutoInvalidationDelay;
            var invalidationDelay = isRemoteServiceMethod
                ? rma?.InvalidationDelay ?? double.NaN
                : a.InvalidationDelay;
            if (rma is not null && rma.ConsolidationDelay is not double.NaN)
                throw new InvalidOperationException(
                    $"{nameof(ConsolidationDelay)} cannot be used with {nameof(RemoteComputeMethodAttribute)}.");
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
        });

    protected virtual bool PrintMembers(StringBuilder sb)
    {
        var ic = CultureInfo.InvariantCulture;
        var initialLength = sb.Length;
        var t = MinCacheDuration;
        if (t != default)
            sb.Append(nameof(MinCacheDuration)).Append(": ").Append(t.ToShortString()).Append(", ");

        t = TransientErrorInvalidationDelay;
        if (t != TimeSpan.MaxValue)
            sb.Append(nameof(TransientErrorInvalidationDelay)).Append(": ").Append(t.ToShortString()).Append(", ");

        t = AutoInvalidationDelay;
        if (t != TimeSpan.MaxValue)
            sb.Append(nameof(AutoInvalidationDelay)).Append(": ").Append(t.ToShortString()).Append(", ");

        t = InvalidationDelay;
        if (t != TimeSpan.MaxValue)
            sb.Append(nameof(InvalidationDelay)).Append(": ").Append(t.ToShortString()).Append(", ");

        t = ConsolidationDelay;
        if (t != TimeSpan.MaxValue)
            sb.Append(nameof(ConsolidationDelay)).Append(": ").Append(t.ToShortString()).Append(", ");

        var m = RemoteComputedCacheMode;
        if (m != default)
            sb.AppendFormat(ic, "{0}: {1:G}, ", nameof(RemoteComputedCacheMode), m);

        // We intentionally don't print CancellationReprocessing here

        if (sb.Length == initialLength)
            return false;

        sb.Length -= 2; // Remove the last comma
        return true;
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
