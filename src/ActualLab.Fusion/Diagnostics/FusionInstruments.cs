using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace ActualLab.Fusion.Diagnostics;

/// <summary>
/// Provides shared <see cref="ActivitySource"/> and <see cref="Meter"/> instances for Fusion diagnostics.
/// </summary>
public static class FusionInstruments
{
    public static readonly ActivitySource ActivitySource = new(ThisAssembly.AssemblyName, ThisAssembly.AssemblyVersion);
    public static readonly Meter Meter = new(ThisAssembly.AssemblyName, ThisAssembly.AssemblyVersion);

    public static readonly Counter<long> OperationRetryCount = Meter.CreateCounter<long>(
        "operation.retry.count", "{retry}", "Count of operation retry outcomes.");
    public static readonly Histogram<double> OperationRetryDelay = Meter.CreateHistogram<double>(
        "operation.retry.delay", "ms", "Delay before operation retries.");
    public static readonly Histogram<double> InvalidationPassDuration = Meter.CreateHistogram<double>(
        "invalidation.pass.duration", "ms", "Duration of deferred invalidation apply passes.");
    public static readonly Histogram<long> InvalidationPassCallCount = Meter.CreateHistogram<long>(
        "invalidation.pass.call.count", "{call}", "Invalidation calls applied per pass.");
    public static readonly Counter<long> DeferredInvalidationFailureCount = Meter.CreateCounter<long>(
        "invalidation.deferred.failure.count", "{failure}",
        "Count of deferred invalidation blocks, and of recorded calls, that threw.");
    public static readonly Counter<long> DeferredInvalidationDropCount = Meter.CreateCounter<long>(
        "invalidation.deferred.drop.count", "{call}", "Count of replicated invalidation calls dropped on apply.");
    public static readonly Counter<long> RemoteComputedCacheRequestCount = Meter.CreateCounter<long>(
        "remote_computed.cache.request.count", "{request}", "Count of persistent remote-computed cache requests.");
    public static readonly Histogram<double> RemoteComputedCacheLookupDuration = Meter.CreateHistogram<double>(
        "remote_computed.cache.lookup.duration", "ms", "Duration of persistent remote-computed cache lookups.");
    public static readonly Counter<long> RemoteComputedCacheStaleValueCount = Meter.CreateCounter<long>(
        "remote_computed.cache.stale_value.count", "{request}", "Count of stale remote-computed values served.");
}
