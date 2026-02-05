using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace ActualLab.Rpc.Diagnostics;

/// <summary>
/// Provides shared OpenTelemetry activity sources, meters, and counters for the RPC framework.
/// </summary>
public static class RpcInstruments
{
    public static readonly ActivitySource ActivitySource = new(ThisAssembly.AssemblyName, ThisAssembly.AssemblyVersion);
    public static readonly Meter Meter = new(ThisAssembly.AssemblyName, ThisAssembly.AssemblyVersion);
    // public static readonly Counter<long> InboundCallCounter;
    public static readonly Counter<long> InboundErrorCounter;
    public static readonly Counter<long> InboundCancellationCounter;
    public static readonly Counter<long> InboundIncompleteCounter;
    public static readonly Histogram<double> InboundDurationHistogram;
    public static bool IsEnabled {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => InboundDurationHistogram.Enabled;
    }

    static RpcInstruments()
    {
        var m = Meter;
        var ms = "rpc";
        var server = $"{ms}.server";
        // See https://opentelemetry.io/docs/specs/semconv/rpc/rpc-metrics/
        // InboundCallCounter = m.CreateCounter<long>($"{server}.call.count",
        //     null, "Count of inbound RPC calls.");
        InboundErrorCounter = m.CreateCounter<long>($"{server}.error.count",
            null, "Count of inbound RPC calls completed with an error.");
        InboundCancellationCounter = m.CreateCounter<long>($"{server}.cancellation.count",
            null, "Count of inbound RPC calls completed with cancellation.");
        InboundIncompleteCounter = m.CreateCounter<long>($"{server}.incomplete.count",
            null, "Count of incomplete inbound RPC calls.");
        InboundDurationHistogram = m.CreateHistogram<double>($"{server}.duration",
            "ms", "Duration of inbound RPC calls.");
    }

    public static void RegisterInboundCall(in RpcCallSummary callSummary)
    {
        // InboundCallCounter.Add(1);
        var resultKind = callSummary.ResultKind;
        if (resultKind == TaskResultKind.Incomplete) {
            InboundIncompleteCounter.Add(1);
            return;
        }
        InboundDurationHistogram.Record(callSummary.DurationMs);
        if (resultKind == TaskResultKind.Success)
            return;

        (resultKind == TaskResultKind.Cancellation
            ? InboundCancellationCounter
            : InboundErrorCounter
            ).Add(1);
    }
}
