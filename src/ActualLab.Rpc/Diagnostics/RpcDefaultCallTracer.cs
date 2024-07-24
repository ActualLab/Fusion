using System.Diagnostics;
using System.Diagnostics.Metrics;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc.Diagnostics;

public class RpcDefaultCallTracer : RpcCallTracer
{
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
        var fullMethodName = DiagnosticsExt.FixName($"{method.Service.Name.Value}.{method.Name.Value}");
        var ms = $"rpc.server.{fullMethodName}";
        CallCounter = m.CreateCounter<long>($"{ms}.call.count",
            null, $"Count of incoming calls of {fullMethodName}.");
        ErrorCounter = m.CreateCounter<long>($"{ms}.error.count",
            null, $"Count of incoming calls of {fullMethodName} completed with an error.");
        CancellationCounter = m.CreateCounter<long>($"{ms}.cancellation.count",
            null, $"Count of cancelled incoming calls  {fullMethodName}.");
        DurationHistogram = m.CreateHistogram<double>($"{ms}.call.duration",
            "ms", $"Duration of incoming calls to {fullMethodName}.");
    }

    public override RpcCallTrace? TryStartTrace(RpcInboundCall call)
        => new RpcDefaultCallTrace(this, ActivitySource.StartActivity(OperationName));
}
