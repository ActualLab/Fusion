using System.Diagnostics;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc.Diagnostics;

public static class RpcActivityInjector
{
    // private static readonly ILogger Log = StaticLog.For(typeof(RpcActivityInjector));

    public static RpcHeader[] Inject(RpcHeader[]? headers, ActivityContext activityContext)
    {
        var (traceParent, traceState) = activityContext.Format();
        var traceParentHeader = new RpcHeader(WellKnownRpcHeaders.W3CTraceParent, traceParent);
        var traceStateHeader = new RpcHeader(WellKnownRpcHeaders.W3CTraceState, traceState);
        return headers.With(traceParentHeader, traceStateHeader);
    }

    public static bool TryExtract(RpcHeader[]? headers, out ActivityContext activityContext)
    {
        var traceParent = headers.TryGet(WellKnownRpcHeaders.W3CTraceParent);
        if (traceParent is null) {
            activityContext = default;
            return false;
        }
        var traceState = headers.TryGet(WellKnownRpcHeaders.W3CTraceState);
        // Log.LogWarning("TryExtract: {TraceParent} | {TraceState}", traceParent, traceState);
#if NET7_0_OR_GREATER
        return ActivityContext.TryParse(traceParent, traceState, true, out activityContext);
#else
        if (!ActivityContext.TryParse(traceParent, traceState, out activityContext))
            return false;

        activityContext = new ActivityContext(
            activityContext.TraceId,
            activityContext.SpanId,
            activityContext.TraceFlags,
            activityContext.TraceState,
            isRemote: true); // That's the only reason we recreate it
        return true;
#endif
    }
}
