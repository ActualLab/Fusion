using System.Diagnostics;
using System.Diagnostics.Metrics;
using ActualLab.Rpc.Infrastructure;
using OpenTelemetry;

namespace ActualLab.Rpc.Diagnostics;

public class RpcDefaultCallTracer : RpcCallTracer
{
    public readonly string InboundCallName;
    public readonly string OutboundCallName;
    public readonly ActivitySource ActivitySource;
    public readonly Counter<long> InboundCallCounter;
    public readonly Counter<long> InboundErrorCounter;
    public readonly Counter<long> InboundCancellationCounter;
    public readonly Histogram<double> InboundDurationHistogram;

    public RpcDefaultCallTracer(RpcMethodDef method) : base(method)
    {
        InboundCallName = $"in.{method.Service.Name.Value}/{method.Name.Value}";
        OutboundCallName = $"out.{method.Service.Name.Value}/{method.Name.Value}";
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
        Activity? activity;
        var propagationContext = RpcPropagationContext.Extract(call.Context.Message.Headers);
        if (propagationContext == default)
            activity = ActivitySource.StartActivity(InboundCallName, ActivityKind.Server);
        else {
            Baggage.Current = propagationContext.Baggage;
            activity = ActivitySource.StartActivity(InboundCallName, ActivityKind.Server,
                parentContext: propagationContext.ActivityContext,
                links: [new ActivityLink(propagationContext.ActivityContext)]);
        }
        return new RpcDefaultInboundCallTrace(this, activity);
    }

    public override RpcOutboundCallTrace? StartOutboundTrace(RpcOutboundCall call)
    {
        // Activity should never become Current
        var lastActivity = Activity.Current;
        var activity = ActivitySource.StartActivity(OutboundCallName, ActivityKind.Client);
        if (lastActivity != activity)
            Activity.Current = lastActivity;
        return new RpcDefaultOutboundCallTrace(activity);
    }
}
