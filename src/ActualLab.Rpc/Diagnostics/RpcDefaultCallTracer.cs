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
    public readonly Counter<long> InboundIncompleteCounter;
    public readonly Histogram<double> InboundDurationHistogram;

    public RpcDefaultCallTracer(RpcMethodDef method, bool traceInbound = true, bool traceOutbound = true)
        : base(method)
    {
        TraceInbound = traceInbound;
        TraceOutbound = traceOutbound;
        var fullMethodName = DiagnosticsExt.FixName($"{method.Service.Name.Value}/{method.Name.Value}");
        InboundCallName = "in." + fullMethodName;
        OutboundCallName = "out." + fullMethodName;
        ActivitySource = method.Hub.ActivitySource;

        var m = RpcMeters.Meter;
        var ms = $"rpc.server.{fullMethodName}";
        InboundCallCounter = m.CreateCounter<long>($"{ms}.call.count",
            null, $"Count of inbound {fullMethodName} calls.");
        InboundErrorCounter = m.CreateCounter<long>($"{ms}.error.count",
            null, $"Count of inbound {fullMethodName} calls completed with an error.");
        InboundCancellationCounter = m.CreateCounter<long>($"{ms}.cancellation.count",
            null, $"Count of inbound {fullMethodName} calls completed with cancellation.");
        InboundIncompleteCounter = m.CreateCounter<long>($"{ms}.incomplete.count",
            null, $"Count of incomplete inbound {fullMethodName} calls.");
        InboundDurationHistogram = m.CreateHistogram<double>($"{ms}.call.duration",
            "ms", $"Duration of inbound {fullMethodName} calls.");
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

    public void RegisterInboundCall(in RpcCallSummary callSummary)
    {
        InboundCallCounter.Add(1);
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
