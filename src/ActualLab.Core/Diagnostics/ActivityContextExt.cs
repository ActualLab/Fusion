using System.Diagnostics;

namespace ActualLab.Diagnostics;

/// <summary>
/// Extension methods for <see cref="ActivityContext"/> to format W3C trace context headers.
/// </summary>
public static class ActivityContextExt
{
    public static (string TraceParent, string TraceState) Format(this ActivityContext activityContext)
    {
        // The code below is based on:
        // - https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/src/OpenTelemetry.Api/Context/Propagation/TraceContextPropagator.cs#L116
#if NET6_0_OR_GREATER
        var traceParent = string.Create(55, activityContext, WriteTraceParentIntoSpan);
#else
        var traceParent = string.Concat(
            "00-", activityContext.TraceId.ToHexString(),
            "-", activityContext.SpanId.ToHexString(),
            (activityContext.TraceFlags & ActivityTraceFlags.Recorded) != 0 ? "-01" : "-00");
#endif
        var traceState = activityContext.TraceState ?? "";
        return (traceParent, traceState);
    }

#if NET6_0_OR_GREATER
    private static void WriteTraceParentIntoSpan(Span<char> span, ActivityContext activityContext)
    {
        "00-".CopyTo(span);
        activityContext.TraceId.ToHexString().CopyTo(span.Slice(3));
        span[35] = '-';
        activityContext.SpanId.ToHexString().CopyTo(span.Slice(36));
        if ((activityContext.TraceFlags & ActivityTraceFlags.Recorded) != 0)
            "-01".CopyTo(span.Slice(52));
        else
            "-00".CopyTo(span.Slice(52));
    }
#endif
}
