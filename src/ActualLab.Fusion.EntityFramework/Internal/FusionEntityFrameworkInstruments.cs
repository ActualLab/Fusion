using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace ActualLab.Fusion.EntityFramework.Internal;

/// <summary>
/// Provides shared OpenTelemetry instrumentation instances
/// for the ActualLab.Fusion.EntityFramework library.
/// </summary>
public static class FusionEntityFrameworkInstruments
{
    public static readonly ActivitySource ActivitySource = new(ThisAssembly.AssemblyName, ThisAssembly.AssemblyVersion);
    public static readonly Meter Meter = new(ThisAssembly.AssemblyName, ThisAssembly.AssemblyVersion);
    public static readonly Histogram<double> OperationLogProcessingDelay;

    static FusionEntityFrameworkInstruments()
    {
        var m = Meter;
        var ms = "db";
        var operationLog = $"{ms}.operation_log";
        OperationLogProcessingDelay = m.CreateHistogram<double>($"{operationLog}.processing.delay",
            "ms", "Delay between logging an operation on its origin host and processing it by the local log reader.");
    }
}
