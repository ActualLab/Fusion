using System.Diagnostics;

namespace ActualLab.Rpc;

public record RpcLimits
{
    public static RpcLimits Default { get; set; } = Debugger.IsAttached ? DebugDefaults : new();
    public static RpcLimits DebugDefaults => new() {
        HandshakeTimeout = TimeSpan.FromSeconds(60),
        KeepAlivePeriod = TimeSpan.FromSeconds(300),
        KeepAliveTimeout = TimeSpan.FromSeconds(1000),
        CallTimeoutCheckPeriod = TimeSpan.FromSeconds(1),
    };

    // Connect timeout; if connecting takes longer, reconnect starts
    public TimeSpan ConnectTimeout { get; init; } = TimeSpan.FromSeconds(10);
    // Handshake timeout; if handshaking takes longer, reconnect starts
    public TimeSpan HandshakeTimeout { get; init; } = TimeSpan.FromSeconds(10);
    // The period peer sends "keep-alive" message, which also tells which of remote objects are still alive
    public TimeSpan KeepAlivePeriod { get; init; } = TimeSpan.FromSeconds(15);
    // When "keep-alive" isn't received during this period, the connection gets dropped -> reconnect starts
    public TimeSpan KeepAliveTimeout { get; init; } = TimeSpan.FromSeconds(55);
    // The code that checks ObjectKeepAliveTimeout & ObjectReleaseTimeout runs w/ this cycle time
    public TimeSpan ObjectReleasePeriod { get; init; } = TimeSpan.FromSeconds(10);
    // When the object doesn't get a "keep-alive" this long, it gets released
    public TimeSpan ObjectReleaseTimeout { get; init; }= TimeSpan.FromSeconds(125);
    // We want to complete "object abort" in this number of cycles.
    // We proceed to the next iteration if at least one new object was disposed during the current one.
    public int ObjectAbortCycleCount { get; init; } = 3;
    // A single "object abort" cycle duration
    public TimeSpan ObjectAbortCyclePeriod { get; init; } = TimeSpan.FromSeconds(1);
    // A single "call abort" cycle period
    public TimeSpan CallAbortCyclePeriod { get; set; } = TimeSpan.FromSeconds(1);
    // Call timeout check period
    public RandomTimeSpan CallTimeoutCheckPeriod { get; init; } = TimeSpan.FromSeconds(5).ToRandom(0.2);
}
