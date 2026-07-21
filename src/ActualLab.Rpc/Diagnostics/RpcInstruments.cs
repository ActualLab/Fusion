using System.Diagnostics;
using System.Diagnostics.Metrics;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc.Diagnostics;

/// <summary>
/// Provides shared OpenTelemetry activity sources, meters, and counters for the RPC framework.
/// </summary>
public static class RpcInstruments
{
    private static long _openInboundPendingCallCount;
    private static long _openInboundResultReadyCallCount;
    private static long _openInboundInvalidatedCallCount;
    private static long _openOutboundPendingCallCount;
    private static long _openOutboundResultReadyCallCount;
    private static long _openOutboundInvalidatedCallCount;

    public static readonly ActivitySource ActivitySource = new(ThisAssembly.AssemblyName, ThisAssembly.AssemblyVersion);
    public static readonly Meter Meter = new(ThisAssembly.AssemblyName, ThisAssembly.AssemblyVersion);

    // public static readonly Counter<long> InboundCallCounter;
    public static readonly Counter<long> InboundErrorCounter;
    public static readonly Counter<long> InboundCancellationCounter;
    public static readonly Counter<long> InboundIncompleteCounter;
    public static readonly Histogram<double> InboundDurationHistogram;
    public static readonly Histogram<double> OutboundDurationHistogram;
    public static readonly Counter<long> OutboundRerouteCounter;
    public static readonly Counter<long> ClientConnectionAttemptCounter;
    public static readonly Histogram<double> ClientConnectionAttemptDurationHistogram;
    public static readonly Histogram<double> ClientConnectionUptimeHistogram;
    public static readonly Histogram<double> ServerConnectionUptimeHistogram;
    public static readonly ObservableGauge<long> OpenInboundCallGauge;
    public static readonly ObservableGauge<long> OpenOutboundCallGauge;
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
        ClientConnectionAttemptCounter = m.CreateCounter<long>($"{ms}.client.connection.attempt.count",
            "{attempt}", "Count of completed client connection attempts.");
        ClientConnectionAttemptDurationHistogram = m.CreateHistogram<double>(
            $"{ms}.client.connection.attempt.duration",
            "ms", "Duration of client connection attempts.");
        ClientConnectionUptimeHistogram = m.CreateHistogram<double>($"{ms}.client.connection.uptime",
            "ms", "Duration of established client connections.");
        ServerConnectionUptimeHistogram = m.CreateHistogram<double>($"{ms}.server.connection.uptime",
            "ms", "Duration of established server connections.");
        OpenInboundCallGauge = m.CreateObservableGauge<long>($"{server}.call.open",
            ObserveOpenInboundCalls, "{call}", "Number of open inbound RPC calls by stage.");
        OpenOutboundCallGauge = m.CreateObservableGauge<long>($"{ms}.client.call.open",
            ObserveOpenOutboundCalls, "{call}", "Number of open outbound RPC calls by stage.");
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
        if (OutboundRerouteCounter.IfEnabled() is not { } rerouteCounter)
            return;

        var tags = new TagList {
            { "rpc.method", methodDef.FullName },
            { "rpc.method.kind", methodDef.Kind.ToString().ToLowerInvariant() },
            { "rpc.routing.mode", routingMode.ToString().ToLowerInvariant() },
        };
        rerouteCounter.Add(1, tags);
    }

    public static void RegisterClientConnectionAttempt(
        RpcClientPeer peer,
        double? durationMs,
        Exception? error,
        bool isCancellationRequested)
    {
        var tags = GetConnectionTags(peer, error, isCancellationRequested);
        ClientConnectionAttemptCounter.Add(1, tags);
        if (durationMs is { } value)
            ClientConnectionAttemptDurationHistogram.Record(value, tags);
    }

    public static void RegisterConnectionUptime(
        RpcPeer peer,
        double durationMs,
        Exception? error,
        bool isCancellationRequested)
    {
        var tags = GetConnectionTags(peer, error, isCancellationRequested);
        var histogram = peer is RpcClientPeer
            ? ClientConnectionUptimeHistogram
            : ServerConnectionUptimeHistogram;
        histogram.Record(durationMs, tags);
    }

    internal static void UpdateOpenInboundCallCounts(RpcCallStageCounts oldValue, RpcCallStageCounts newValue)
    {
        Interlocked.Add(ref _openInboundPendingCallCount, newValue.Pending - oldValue.Pending);
        Interlocked.Add(ref _openInboundResultReadyCallCount, newValue.ResultReady - oldValue.ResultReady);
        Interlocked.Add(ref _openInboundInvalidatedCallCount, newValue.Invalidated - oldValue.Invalidated);
    }

    internal static void UpdateOpenOutboundCallCounts(RpcCallStageCounts oldValue, RpcCallStageCounts newValue)
    {
        Interlocked.Add(ref _openOutboundPendingCallCount, newValue.Pending - oldValue.Pending);
        Interlocked.Add(ref _openOutboundResultReadyCallCount, newValue.ResultReady - oldValue.ResultReady);
        Interlocked.Add(ref _openOutboundInvalidatedCallCount, newValue.Invalidated - oldValue.Invalidated);
    }

    public static void RegisterClientCallEvents(int delayedCount, int resendCount, int timeoutCount)
    {
        if (ClientCallEventCounter.IfEnabled() is not { } callEventCounter)
            return;

        if (delayedCount > 0)
            callEventCounter.Add(delayedCount, new KeyValuePair<string, object?>("rpc.call.event", "delayed"));
        if (resendCount > 0)
            callEventCounter.Add(resendCount, new KeyValuePair<string, object?>("rpc.call.event", "resend"));
        if (timeoutCount > 0)
            callEventCounter.Add(timeoutCount, new KeyValuePair<string, object?>("rpc.call.event", "timeout"));
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
            { "rpc.connection.kind", peer.ConnectionKind.ToString().ToLowerInvariant() },
            { "outcome", outcome },
        };
    }

    private static IEnumerable<Measurement<long>> ObserveOpenInboundCalls()
    {
        yield return new(Volatile.Read(ref _openInboundPendingCallCount),
            new KeyValuePair<string, object?>("rpc.call.stage", "pending"));
        yield return new(Volatile.Read(ref _openInboundResultReadyCallCount),
            new KeyValuePair<string, object?>("rpc.call.stage", "result_ready"));
        yield return new(Volatile.Read(ref _openInboundInvalidatedCallCount),
            new KeyValuePair<string, object?>("rpc.call.stage", "invalidated"));
    }

    private static IEnumerable<Measurement<long>> ObserveOpenOutboundCalls()
    {
        yield return new(Volatile.Read(ref _openOutboundPendingCallCount),
            new KeyValuePair<string, object?>("rpc.call.stage", "pending"));
        yield return new(Volatile.Read(ref _openOutboundResultReadyCallCount),
            new KeyValuePair<string, object?>("rpc.call.stage", "result_ready"));
        yield return new(Volatile.Read(ref _openOutboundInvalidatedCallCount),
            new KeyValuePair<string, object?>("rpc.call.stage", "invalidated"));
    }
}
