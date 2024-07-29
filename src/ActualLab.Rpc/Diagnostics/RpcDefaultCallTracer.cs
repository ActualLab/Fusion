using System.Diagnostics;
using System.Diagnostics.Metrics;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc.Diagnostics;

public class RpcDefaultCallTracer : RpcCallTracer
{
    public readonly string OperationName;
    public readonly ActivitySource ActivitySource;
    public readonly Counter<long> InboundCallCounter;
    public readonly Counter<long> InboundErrorCounter;
    public readonly Counter<long> InboundCancellationCounter;
    public readonly Histogram<double> InboundDurationHistogram;

    public RpcDefaultCallTracer(RpcMethodDef method) : base(method)
    {
        OperationName = $"{method.Service.Name.Value}/{method.Name.Value}";
        ActivitySource = method.Hub.ActivitySource;

        var m = RpcMeters.Meter;
        var fullMethodName = DiagnosticsExt.FixName($"{method.Service.Name.Value}.{method.Name.Value}");
        var ms = $"rpc.server.{fullMethodName}";
        InboundCallCounter = m.CreateCounter<long>($"{ms}.call.count",
            null, $"Count of incoming calls of {fullMethodName}.");
        InboundErrorCounter = m.CreateCounter<long>($"{ms}.error.count",
            null, $"Count of incoming calls of {fullMethodName} completed with an error.");
        InboundCancellationCounter = m.CreateCounter<long>($"{ms}.cancellation.count",
            null, $"Count of cancelled incoming calls  {fullMethodName}.");
        InboundDurationHistogram = m.CreateHistogram<double>($"{ms}.call.duration",
            "ms", $"Duration of incoming calls to {fullMethodName}.");
    }

    public override RpcInboundCallTrace? StartInboundTrace(RpcInboundCall call)
    {
        var activity = call.Context.Message.Headers.ExtractActivity(OperationName);
        return new RpcDefaultInboundCallTrace(this, activity);
    }

    public override RpcOutboundCallTrace? StartOutboundTrace(RpcOutboundCall call)
    {
        var activity = ActivitySource.StartActivity(OperationName, ActivityKind.Client);
        return new RpcDefaultOutboundCallTrace(activity);
    }
}
