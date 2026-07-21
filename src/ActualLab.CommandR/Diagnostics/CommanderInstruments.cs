using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace ActualLab.CommandR.Diagnostics;

/// <summary>
/// Provides OpenTelemetry instrumentation primitives for the commander pipeline.
/// </summary>
public static class CommanderInstruments
{
    public static readonly ActivitySource ActivitySource = new(ThisAssembly.AssemblyName, ThisAssembly.AssemblyVersion);
    public static readonly Meter Meter = new(ThisAssembly.AssemblyName, ThisAssembly.AssemblyVersion);
    public static readonly Histogram<double> CommandExecutionDuration = Meter.CreateHistogram<double>(
        "command.execution.duration", "ms", "Duration of command executions.");
}
