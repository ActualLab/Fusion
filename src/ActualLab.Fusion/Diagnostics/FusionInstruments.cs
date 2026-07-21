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
        "invalidation.pass.duration", "ms", "Duration of invalidation replay passes.");
    public static readonly Histogram<long> InvalidationPassCommandCount = Meter.CreateHistogram<long>(
        "invalidation.pass.command.count", "{command}", "Commands attempted per invalidation replay pass.");
    public static readonly Counter<long> RemoteComputedCacheRequestCount = Meter.CreateCounter<long>(
        "remote_computed.cache.request.count", "{request}", "Count of persistent remote-computed cache requests.");
    public static readonly Histogram<double> RemoteComputedCacheLookupDuration = Meter.CreateHistogram<double>(
        "remote_computed.cache.lookup.duration", "ms", "Duration of persistent remote-computed cache lookups.");
    public static readonly Counter<long> RemoteComputedCacheStaleValueCount = Meter.CreateCounter<long>(
        "remote_computed.cache.stale_value.count", "{request}", "Count of stale remote-computed values served.");
}
