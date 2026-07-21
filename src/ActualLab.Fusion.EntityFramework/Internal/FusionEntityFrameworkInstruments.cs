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

    public static readonly Histogram<double> EventLogProcessingDelay;
    public static readonly Histogram<int> LogBatchSize;
    public static readonly Histogram<double> LogBatchDuration;
    public static readonly Histogram<double> OperationLogProcessingDelay;

    static FusionEntityFrameworkInstruments()
    {
        var m = Meter;
        var ms = "db";
        var eventLog = $"{ms}.event_log";
        EventLogProcessingDelay = m.CreateHistogram<double>($"{eventLog}.processing.delay",
            "ms", "Delay between an event becoming eligible and processing it by the local log reader.");
        var logBatch = $"{ms}.log.batch";
        LogBatchSize = m.CreateHistogram<int>($"{logBatch}.size",
            "{entry}", "Number of entries read in a database log batch.");
        LogBatchDuration = m.CreateHistogram<double>($"{logBatch}.duration",
            "ms", "Database log batch processing duration.");
        var operationLog = $"{ms}.operation_log";
        OperationLogProcessingDelay = m.CreateHistogram<double>($"{operationLog}.processing.delay",
            "ms", "Delay between logging an operation on its origin host and processing it by the local log reader.");
    }
}
