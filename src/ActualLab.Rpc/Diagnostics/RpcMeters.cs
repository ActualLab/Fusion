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
        // See https://opentelemetry.io/docs/specs/semconv/rpc/rpc-metrics/
        ServerCallCounter = m.CreateCounter<long>($"{ms}.call.count",
            null, "Call count.");
        ServerErrorCounter = m.CreateCounter<long>($"{ms}.error.count",
            null, "Error count.");
        ServerCancellationCounter = m.CreateCounter<long>($"{ms}.cancellation.count",
            null, "Cancellation count.");
        ServerDurationHistogram = m.CreateHistogram<double>($"{ms}.server.duration",
            "ms", "Duration of inbound RPC calls.");
    }
}
