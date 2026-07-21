using System.Diagnostics;
using System.Diagnostics.Metrics;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc.Diagnostics;

/// <summary>
/// Default <see cref="RpcCallTracer"/> that produces OpenTelemetry activities and records call metrics.
/// </summary>
public class RpcDefaultCallTracer : RpcCallTracer
{
    public readonly bool MustTraceInbound;
    public readonly bool MustTraceOutbound;
    public readonly string InboundCallName;
    public readonly string OutboundCallName;
    public readonly ActivitySource ActivitySource;
    public readonly KeyValuePair<string, object?>[] ActivityTags;
    // public readonly Counter<long> InboundCallCounter;
    public readonly Counter<long> InboundErrorCounter;
    public readonly Counter<long> InboundCancellationCounter;
    public readonly Counter<long> InboundIncompleteCounter;
    public readonly Histogram<double> InboundDurationHistogram;
    public bool IsEnabled {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => RpcInstruments.IsInboundEnabled;
    }

    public RpcDefaultCallTracer(RpcMethodDef methodDef, bool mustTraceInbound = true, bool mustTraceOutbound = true)
        : base(methodDef)
    {
        MustTraceInbound = mustTraceInbound;
        MustTraceOutbound = mustTraceOutbound;
        var fullMethodName = DiagnosticsExt.FixName($"{methodDef.Service.Name}/{methodDef.Name}");
        InboundCallName = "in." + fullMethodName;
        OutboundCallName = "out." + fullMethodName;
        ActivitySource = RpcInstruments.ActivitySource;
        ActivityTags = [
            new("rpc.system.name", "actuallab.rpc"),
            new("rpc.method", methodDef.FullName),
        ];

        InboundErrorCounter = RpcInstruments.InboundErrorCounter;
        InboundCancellationCounter = RpcInstruments.InboundCancellationCounter;
        InboundIncompleteCounter = RpcInstruments.InboundIncompleteCounter;
        InboundDurationHistogram = RpcInstruments.InboundDurationHistogram;
    }

    public override RpcInboundCallTrace? StartInboundTrace(RpcInboundCall call)
    {
        if (!MustTraceInbound)
            return null;

        Activity.Current = null; // No current activity for any inbound call
        var headers = call.Context.Message.Headers;
        var activity = headers is not null && RpcActivityInjector.TryExtract(headers, out var activityContext)
            ? ActivitySource.StartActivity(InboundCallName, ActivityKind.Server,
                parentContext: activityContext, tags: ActivityTags)
            : ActivitySource.StartActivity(InboundCallName, ActivityKind.Server,
                parentContext: default(ActivityContext), tags: ActivityTags);
        if (activity is null && !IsEnabled)
            return null;
        return new RpcDefaultInboundCallTrace(this, activity);
    }

    public override RpcOutboundCallTrace? StartOutboundTrace(
        RpcOutboundCall call,
        ActivityContext parentActivityContext)
    {
        if (!MustTraceOutbound)
            return null;

        // Activity should never become Current
        var lastActivity = Activity.Current;
        var activity = ActivitySource.StartActivity(
            OutboundCallName, ActivityKind.Client, parentContext: parentActivityContext, tags: ActivityTags);
        if (lastActivity != activity)
            Activity.Current = lastActivity;
        return activity is null ? null : new RpcDefaultOutboundCallTrace(activity, parentActivityContext);
    }

    public void RegisterInboundCall(in RpcCallSummary callSummary, Exception? error)
        => RpcInstruments.RegisterInboundCall(MethodDef, callSummary, error);
}
