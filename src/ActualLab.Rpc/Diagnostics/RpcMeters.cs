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
            null, "Count of incoming RPC calls.");
        ServerErrorCounter = m.CreateCounter<long>($"{server}.error.count",
            null, "Count of incoming RPC calls completed with an error.");
        ServerCancellationCounter = m.CreateCounter<long>($"{server}.cancellation.count",
            null, "Count of cancelled incoming RPC calls.");
        ServerDurationHistogram = m.CreateHistogram<double>($"{server}.duration",
            "ms", "Duration of incoming RPC calls.");
    }
}
