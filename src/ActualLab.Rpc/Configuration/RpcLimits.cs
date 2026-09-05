using System.Diagnostics;
using ActualLab.OS;

namespace ActualLab.Rpc;

/// <summary>
/// Defines timeout and periodic limits for RPC connections, keep-alive, and object lifecycle.
/// </summary>
public record RpcLimits
{
    public static RpcLimits Default { get; set; } = new(Debugger.IsAttached);

    // Connect timeout; if connecting takes longer, reconnect starts
    public TimeSpan ConnectTimeout { get; init; } = TimeSpan.FromSeconds(10);
    // Handshake timeout; if handshaking takes longer, reconnect starts
    public TimeSpan HandshakeTimeout { get; init; } = TimeSpan.FromSeconds(10);
    // If the connection was alive for less than this, graceful close still bumps ConnectionAttemptIndex
    public TimeSpan PrematureDisconnectTimeout { get; init; } = TimeSpan.FromSeconds(15);
    // The period peer sends "keep-alive" message, which also tells which of remote objects are still alive
    public TimeSpan KeepAlivePeriod { get; init; } = TimeSpan.FromSeconds(10);
    // When "keep-alive" isn't received during this period, the connection gets dropped -> reconnect starts.
    // Sized to tolerate a complete ~15s server stall plus most of one keepalive cycle on top:
    //   worst-case age of LastKeepAliveAt = KeepAlivePeriod + stall (a stall that starts right after
    //   the last successful keepalive delays the next one by Period + stall_duration).
    public TimeSpan KeepAliveTimeout { get; init; } = TimeSpan.FromSeconds(25);
    // The code that checks KeepAliveTimeout & ObjectReleaseTimeout runs w/ this cycle time
    public TimeSpan ObjectReleasePeriod { get; init; } = TimeSpan.FromSeconds(10);
    // When the object doesn't get a "keep-alive" this long, it gets released
    public TimeSpan ObjectReleaseTimeout { get; init; } = TimeSpan.FromSeconds(125);
    // We want to complete "object abort" in this number of cycles.
    // We proceed to the next iteration if at least one new object was disposed during the current one.
    public int ObjectAbortCycleCount { get; init; } = 3;
    // A single "object abort" cycle duration
    public TimeSpan ObjectAbortCyclePeriod { get; init; } = TimeSpan.FromSeconds(1);
    // A single "call abort" cycle period
    public TimeSpan CallAbortCyclePeriod { get; set; } = TimeSpan.FromSeconds(1);
    // Backstop cap on RpcPeer.InboundCalls.Count + RpcPeer.OutboundCalls.Count.
    // It's checked once per ObjectReleasePeriod, and the peer is reset when it's exceeded,
    // so the actual count may overshoot it by up to a cycle's worth of calls.
    // NoWait calls are never registered in either tracker (see RpcInboundCallTracker.GetOrRegister),
    // so they're invisible to this cap - lowering it doesn't throttle a NoWait flood.
    // The default disables the cap: a Fusion server retains one inbound call per live client
    // subscription, so 100K+ open inbound calls is normal operation rather than a leak.
    public int CallCountLimit { get; init; } = int.MaxValue;
    // Backstop cap on RpcPeer.SharedObjects.Count + RpcPeer.RemoteObjects.Count; same cycle
    // and same reset behavior as CallCountLimit. Shared objects are released only after
    // ObjectReleaseTimeout of silence, so a peer abandoning streams stays near the cap
    // (and thus gets reset) roughly once per that timeout.
    public int ObjectCountLimit { get; init; } = 65536;
    // Call timeout check period
    public RandomTimeSpan CallTimeoutCheckPeriod { get; init; } = TimeSpan.FromSeconds(5).ToRandom(0.2);
    // How often a disconnected peer wakes up when nothing else did. Registering a call and
    // rescheduling a reconnect both signal it directly, and known deadlines are slept on exactly,
    // so this tick is a safety poll: a few comparisons, then back to sleep unless one of those
    // says otherwise. It costs one timer per disconnected peer - a connected one runs no such loop.
    public RandomTimeSpan DisconnectCheckPeriod { get; init; } = TimeSpan.FromSeconds(1).ToRandom(0.2);
    public int LogDelayedCallLimit { get; init; } = 10;
    // Outbound call summary logging
    public (int MinCount, TimeSpan Period) LogCallSummarySettings { get; init; }
        = RuntimeInfo.IsServer
            ? (1000, TimeSpan.FromMinutes(10))
            : (1, TimeSpan.FromMinutes(1));

    public RpcLimits(bool useDebugDefaults)
    {
        if (!useDebugDefaults)
            return;

        HandshakeTimeout = TimeSpan.FromSeconds(60);
        KeepAlivePeriod = TimeSpan.FromSeconds(300);
        KeepAliveTimeout = TimeSpan.FromSeconds(1000);
    }
}
