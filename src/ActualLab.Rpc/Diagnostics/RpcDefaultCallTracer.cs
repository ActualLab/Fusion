using System.Diagnostics;
using System.Diagnostics.Metrics;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc.Diagnostics;

public class RpcDefaultCallTracer : RpcCallTracer
{
    protected readonly object Lock = new();

    public readonly string OperationName;
    public readonly ActivitySource ActivitySource;
    public readonly Counter<long> CallCounter;
    public readonly Counter<long> ErrorCounter;
    public readonly Counter<long> CancellationCounter;
    public readonly Histogram<double> DurationHistogram;

    public RpcDefaultCallTracer(RpcMethodDef method) : base(method)
    {
        OperationName = $"{method.Service.Name.Value}/{method.Name.Value}";
        ActivitySource = method.Hub.ActivitySource;

        var m = RpcMeters.Meter;
        var ns = $"rpc.server.{method.Service.Name.Value}.{method.Name.Value}";
        CallCounter = m.CreateCounter<long>($"{ns}.call.count", null, "Call count.");
        ErrorCounter = m.CreateCounter<long>($"{ns}.error.count", null, "Error count.");
        CancellationCounter = m.CreateCounter<long>($"{ns}.cancellation.count", null, "Error count.");
        DurationHistogram = m.CreateHistogram<double>($"{ns}.call.duration", "ms", "Call duration.");
    }

    public override RpcCallTrace? TryStartTrace(RpcInboundCall call)
        => new RpcDefaultCallTrace(this, ActivitySource.StartActivity(OperationName));
}
