using System.Diagnostics;
using System.Diagnostics.Metrics;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc.Diagnostics;

public class RpcDefaultCallTracer : RpcCallTracer
{
    public readonly bool TraceInbound;
    public readonly bool TraceOutbound;
    public readonly string InboundCallName;
    public readonly string OutboundCallName;
    public readonly ActivitySource ActivitySource;
    public readonly Counter<long> InboundCallCounter;
    public readonly Counter<long> InboundErrorCounter;
    public readonly Counter<long> InboundCancellationCounter;
    public readonly Histogram<double> InboundDurationHistogram;

    public RpcDefaultCallTracer(RpcMethodDef method, bool traceInbound = true, bool traceOutbound = true)
        : base(method)
    {
        TraceInbound = traceInbound;
        TraceOutbound = traceOutbound;
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
        if (!TraceInbound)
            return null;

        var headers = call.Context.Message.Headers;
        var activity = headers != null && RpcActivityInjector.TryExtract(headers, out var activityContext)
            ? ActivitySource.StartActivity(InboundCallName, ActivityKind.Server,
                parentContext: activityContext,
                links: [new ActivityLink(activityContext)])
            : ActivitySource.StartActivity(InboundCallName, ActivityKind.Server);
        return new RpcDefaultInboundCallTrace(this, activity);
    }

    public override RpcOutboundCallTrace? StartOutboundTrace(RpcOutboundCall call)
    {
        if (!TraceOutbound)
            return null;

        // Activity should never become Current
        var lastActivity = Activity.Current;
        var activity = ActivitySource.StartActivity(OutboundCallName, ActivityKind.Client);
        if (lastActivity != activity)
            Activity.Current = lastActivity;
        return new RpcDefaultOutboundCallTrace(activity);
    }
}
