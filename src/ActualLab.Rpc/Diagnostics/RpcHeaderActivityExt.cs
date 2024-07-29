using System.Diagnostics;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc.Diagnostics;

public static class RpcHeaderActivityExt
{
    public static RpcHeader[]? InjectActivity(this RpcHeader[]? headers, Activity activity)
    {
        if (activity.IdFormat == ActivityIdFormat.W3C)
            headers = headers.With(
                new(RpcHeaderNames.W3CTraceParent, activity.Id),
                new(RpcHeaderNames.W3CTraceState, activity.TraceStateString)
            );
        else
            headers = headers.With(new(RpcHeaderNames.ActivityId, activity.Id));
        return headers;
    }

    public static Activity ExtractActivity(this RpcHeader[]? headers, string operationName)
    {
        var activity = new Activity(operationName);
        var parentId = headers.TryGet(RpcHeaderNames.W3CTraceParent)
            ?? headers.TryGet(RpcHeaderNames.ActivityId);

        if (!parentId.IsNullOrEmpty()) {
            activity.SetParentId(parentId);
            var traceState = headers.TryGet(RpcHeaderNames.W3CTraceState);
            if (!traceState.IsNullOrEmpty())
                activity.TraceStateString = traceState;
        }

        // The current activity gets an ID with the W3C format
        activity.Start();
        return activity;
    }
}
