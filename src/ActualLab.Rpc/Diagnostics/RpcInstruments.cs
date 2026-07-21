using System.Diagnostics;
using System.Diagnostics.Metrics;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc.Diagnostics;

/// <summary>
/// Provides shared OpenTelemetry activity sources, meters, and counters for the RPC framework.
/// </summary>
public static class RpcInstruments
{
    private static readonly ConditionalWeakTable<RpcInboundCallTracker, object> InboundCallTrackers = new();
    private static readonly ConditionalWeakTable<RpcOutboundCallTracker, object> OutboundCallTrackers = new();
    private static readonly object CallTrackerTag = new();

    public static readonly ActivitySource ActivitySource = new(ThisAssembly.AssemblyName, ThisAssembly.AssemblyVersion);
    public static readonly Meter Meter = new(ThisAssembly.AssemblyName, ThisAssembly.AssemblyVersion);
    // public static readonly Counter<long> InboundCallCounter;
    public static readonly Counter<long> InboundErrorCounter;
    public static readonly Counter<long> InboundCancellationCounter;
    public static readonly Counter<long> InboundIncompleteCounter;
    public static readonly Histogram<double> InboundDurationHistogram;
    public static readonly Histogram<double> OutboundDurationHistogram;
    public static readonly Counter<long> OutboundRerouteCounter;
    public static readonly Counter<long> ConnectionAttemptCounter;
    public static readonly Histogram<double> ConnectionAttemptDurationHistogram;
    public static readonly Histogram<double> ConnectionUptimeHistogram;
    public static readonly ObservableGauge<long> ActiveInboundCallGauge;
    public static readonly ObservableGauge<long> ActiveOutboundCallGauge;
    public static readonly Counter<long> ClientCallEventCounter;
    public static bool IsInboundEnabled {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => InboundDurationHistogram.Enabled
            || InboundErrorCounter.Enabled
            || InboundCancellationCounter.Enabled
            || InboundIncompleteCounter.Enabled;
    }
    public static bool IsOutboundEnabled {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => OutboundDurationHistogram.Enabled;
    }

    static RpcInstruments()
    {
        var m = Meter;
        var ms = "rpc";
        var server = $"{ms}.server";
        // See https://opentelemetry.io/docs/specs/semconv/rpc/rpc-metrics/
        // InboundCallCounter = m.CreateCounter<long>($"{server}.call.count",
        //     null, "Count of inbound RPC calls.");
        InboundErrorCounter = m.CreateCounter<long>($"{server}.error.count",
            null, "Count of inbound RPC calls completed with an error.");
        InboundCancellationCounter = m.CreateCounter<long>($"{server}.cancellation.count",
            null, "Count of inbound RPC calls completed with cancellation.");
        InboundIncompleteCounter = m.CreateCounter<long>($"{server}.incomplete.count",
            null, "Count of incomplete inbound RPC calls.");
        InboundDurationHistogram = m.CreateHistogram<double>($"{server}.call.duration",
            "ms", "Duration of inbound RPC calls.");
        OutboundDurationHistogram = m.CreateHistogram<double>($"{ms}.client.call.duration",
            "ms", "Duration of outbound RPC calls.");
        OutboundRerouteCounter = m.CreateCounter<long>($"{ms}.client.reroute.count",
            "{reroute}", "Count of outbound RPC call reroutes.");
        ConnectionAttemptCounter = m.CreateCounter<long>($"{ms}.connection.attempt.count",
            "{attempt}", "Count of completed RPC connection attempts.");
        ConnectionAttemptDurationHistogram = m.CreateHistogram<double>($"{ms}.connection.attempt.duration",
            "ms", "Duration of RPC connection attempts.");
        ConnectionUptimeHistogram = m.CreateHistogram<double>($"{ms}.connection.uptime",
            "ms", "Duration of established RPC connections.");
        ActiveInboundCallGauge = m.CreateObservableGauge<long>($"{server}.call.active",
            ObserveActiveInboundCalls, "{call}", "Number of active inbound RPC calls.");
        ActiveOutboundCallGauge = m.CreateObservableGauge<long>($"{ms}.client.call.active",
            ObserveActiveOutboundCalls, "{call}", "Number of active outbound RPC calls.");
        ClientCallEventCounter = m.CreateCounter<long>($"{ms}.client.call.event.count",
            "{event}", "Count of client call maintenance events.");
    }

    public static void RegisterInboundCall(
        RpcMethodDef methodDef,
        in RpcCallSummary callSummary,
        Exception? error)
    {
        var tags = GetCallTags(methodDef, error);
        var resultKind = callSummary.ResultKind;
        if (resultKind == TaskResultKind.Incomplete) {
            InboundIncompleteCounter.Add(1, tags);
            return;
        }
        InboundDurationHistogram.Record(callSummary.DurationMs, tags);
        if (resultKind == TaskResultKind.Success)
            return;

        (resultKind == TaskResultKind.Cancellation
            ? InboundCancellationCounter
            : InboundErrorCounter
            ).Add(1, tags);
    }

    public static void RegisterOutboundCall(RpcMethodDef methodDef, double durationMs, Exception? error)
    {
        var tags = GetCallTags(methodDef, error);
        OutboundDurationHistogram.Record(durationMs, tags);
    }

    public static void RegisterReroute(RpcMethodDef methodDef, RpcRoutingMode routingMode)
    {
        if (!OutboundRerouteCounter.Enabled)
            return;

        var tags = new TagList {
            { "rpc.method", methodDef.FullName },
            { "rpc.method.kind", methodDef.Kind.ToString().ToLowerInvariant() },
            { "rpc.routing.mode", routingMode.ToString().ToLowerInvariant() },
        };
        OutboundRerouteCounter.Add(1, tags);
    }

    public static void RegisterConnectionAttempt(
        RpcPeer peer,
        double? durationMs,
        Exception? error,
        bool isCancellationRequested)
    {
        var tags = GetConnectionTags(peer, error, isCancellationRequested);
        ConnectionAttemptCounter.Add(1, tags);
        if (durationMs is { } value)
            ConnectionAttemptDurationHistogram.Record(value, tags);
    }

    public static void RegisterConnectionUptime(
        RpcPeer peer,
        double durationMs,
        Exception? error,
        bool isCancellationRequested)
    {
        var tags = GetConnectionTags(peer, error, isCancellationRequested);
        ConnectionUptimeHistogram.Record(durationMs, tags);
    }

    public static void RegisterCallTracker(RpcInboundCallTracker tracker)
        => InboundCallTrackers.Add(tracker, CallTrackerTag);

    public static void RegisterCallTracker(RpcOutboundCallTracker tracker)
        => OutboundCallTrackers.Add(tracker, CallTrackerTag);

    public static void RegisterClientCallEvents(int delayedCount, int resendCount, int timeoutCount)
    {
        if (!ClientCallEventCounter.Enabled)
            return;

        if (delayedCount > 0)
            ClientCallEventCounter.Add(delayedCount, new KeyValuePair<string, object?>("rpc.call.event", "delayed"));
        if (resendCount > 0)
            ClientCallEventCounter.Add(resendCount, new KeyValuePair<string, object?>("rpc.call.event", "resend"));
        if (timeoutCount > 0)
            ClientCallEventCounter.Add(timeoutCount, new KeyValuePair<string, object?>("rpc.call.event", "timeout"));
    }

    private static TagList GetCallTags(RpcMethodDef methodDef, Exception? error)
    {
        var tags = new TagList {
            { "rpc.system.name", "actuallab.rpc" },
            { "rpc.method", methodDef.FullName },
        };
        if (error is not null && error is not OperationCanceledException)
            tags.Add("error.type", error.GetType().FullName);
        return tags;
    }

    private static TagList GetConnectionTags(
        RpcPeer peer,
        Exception? error,
        bool isCancellationRequested)
    {
        var outcome = isCancellationRequested || error is OperationCanceledException
            ? "cancel"
            : error is null ? "success" : "error";
        return new TagList {
            { "rpc.peer.type", peer is RpcClientPeer ? "client" : "server" },
            { "rpc.connection.kind", peer.ConnectionKind.ToString().ToLowerInvariant() },
            { "outcome", outcome },
        };
    }

    private static long ObserveActiveInboundCalls()
    {
        var count = 0L;
        foreach (var (tracker, _) in InboundCallTrackers)
            count += tracker.Count;
        return count;
    }

    private static long ObserveActiveOutboundCalls()
    {
        var count = 0L;
        foreach (var (tracker, _) in OutboundCallTrackers)
            count += tracker.Count;
        return count;
    }
}
