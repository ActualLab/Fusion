using System.Diagnostics;
using ActualLab.Rpc.Infrastructure;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;

namespace ActualLab.Rpc.Diagnostics;

public static class RpcPropagationContext
{
    public static PropagationContext Extract(RpcHeader[]? headers)
        => Propagators.DefaultTextMapPropagator
            .Extract(default, headers,
                static (headers, name) => headers.TryGet(name) is { } v ? [v] : []);

    public static RpcHeader[]? Inject(RpcHeader[]? headers, Activity activity)
    {
        var propagationContext = new PropagationContext(activity.Context, Baggage.Current);
        var newHeaders = new List<RpcHeader>();
        Propagators.DefaultTextMapPropagator
            .Inject(propagationContext, newHeaders,
                static (headers, key, value) => headers.Add(new RpcHeader(key, value)));
        return headers.WithMany(newHeaders);
    }

#if false // Old code
    public static RpcHeader[]? InjectActivity(RpcHeader[]? headers, Activity activity)
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

    public static Activity ExtractActivity(RpcHeader[]? headers, string operationName)
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
#endif
}
