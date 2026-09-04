namespace ActualLab.Rpc;

/// <summary>
/// Defines connect, run, delay, and reconnect timeouts for outbound RPC calls.
/// </summary>
public sealed partial record RpcCallTimeouts
{
    public static TimeSpan DefaultDelayTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public static readonly RpcCallTimeouts None = new(); // Should go after DefaultDelayTimeout, coz it uses it!

    /// <summary>
    /// How long a call issued while the peer is not connected waits for the connection.
    /// On expiry the call fails with <see cref="RpcTimeoutException"/> of <see cref="RpcTimeoutKind.Connect"/> kind.
    /// </summary>
    public TimeSpan ConnectTimeout { get; init => field = value.Positive(); }
    /// <summary>
    /// How long a sent call waits for its result while the peer is connected; the clock restarts
    /// when the call is resent on reconnect. On expiry the call fails with <see cref="RpcTimeoutException"/>
    /// of <see cref="RpcTimeoutKind.Run"/> kind.
    /// </summary>
    public TimeSpan RunTimeout { get; init => field = value.Positive(); }
    /// <summary>
    /// How long a call waits for a peer that lost its connection to reconnect - when the peer is
    /// disconnected at call time (and has been connected before) and when the connection drops mid-call.
    /// On expiry a remote compute call with a cached value serves it; any other call fails with
    /// <see cref="RpcTimeoutException"/> of <see cref="RpcTimeoutKind.Reconnect"/> kind.
    /// Zero (the default) serves a cached value at once and lets other calls wait indefinitely.
    /// </summary>
    public TimeSpan ReconnectTimeout { get; init => field = value.Positive(); }
    /// <summary>
    /// After how long a still-pending call is reported as delayed. What happens then is up to
    /// <see cref="RpcDelayedCallAction"/>: log (the default), abort with <see cref="RpcTimeoutException"/>
    /// of <see cref="RpcTimeoutKind.Delay"/> kind, or resend.
    /// </summary>
    public TimeSpan DelayTimeout { get; init => field = value.Positive(); } = DefaultDelayTimeout;

    // TimeSpan overloads

    public RpcCallTimeouts()
        : this(TimeSpan.MaxValue, TimeSpan.MaxValue)
    { }

    public RpcCallTimeouts(TimeSpan runTimeout)
        : this(TimeSpan.MaxValue, runTimeout.Positive())
    { }

    public RpcCallTimeouts(TimeSpan connectTimeout, TimeSpan runTimeout)
    {
        ConnectTimeout = connectTimeout;
        RunTimeout = runTimeout;
    }

    // TimeSpan? overloads

    public RpcCallTimeouts(TimeSpan? runTimeout)
        : this(TimeSpan.MaxValue, ToTimeout(runTimeout))
    { }

    // double? overloads

    public RpcCallTimeouts(double? runTimeout)
        : this(TimeSpan.MaxValue, ToTimeout(runTimeout))
    { }

    public RpcCallTimeouts(double? connectTimeout, double? runTimeout)
        : this(ToTimeout(connectTimeout), ToTimeout(runTimeout))
    { }

    // Private methods

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static TimeSpan ToTimeout(TimeSpan? timeout)
        => timeout ?? TimeSpan.MaxValue;

    private static TimeSpan ToTimeout(double? timeout)
        => timeout is { } value and not double.NaN and not double.PositiveInfinity
            ? TimeSpan.FromSeconds(value)
            : TimeSpan.MaxValue;
}
