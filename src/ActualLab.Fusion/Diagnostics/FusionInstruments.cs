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
}
