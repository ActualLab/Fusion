using System.Diagnostics.Metrics;

namespace ActualLab.Rpc.Diagnostics;

public static class RpcMeters
{
    public static readonly Meter Meter;
    public static readonly Counter<long> ServerCallCounter;
    public static readonly Counter<long> ServerErrorCounter;
    public static readonly Counter<long> ServerCancellationCounter;
    public static readonly Histogram<double> ServerDurationHistogram;

    static RpcMeters()
    {
        var m = Meter = typeof(RpcHub).GetMeter();
        var ms = "rpc";
        var server = $"{ms}.server";
        // See https://opentelemetry.io/docs/specs/semconv/rpc/rpc-metrics/
        ServerCallCounter = m.CreateCounter<long>($"{server}.call.count",
            null, "Call count.");
        ServerErrorCounter = m.CreateCounter<long>($"{server}.error.count",
            null, "Error count.");
        ServerCancellationCounter = m.CreateCounter<long>($"{server}.cancellation.count",
            null, "Cancellation count.");
        ServerDurationHistogram = m.CreateHistogram<double>($"{server}.duration",
            "ms", "Duration of inbound RPC calls.");
    }
}
