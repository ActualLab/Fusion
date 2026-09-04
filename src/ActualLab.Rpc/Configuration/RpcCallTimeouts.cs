using System.Text;

namespace ActualLab.Rpc;

/// <summary>
/// Defines the connect, run and delay timeouts of an outbound RPC call, plus its cache fallback delay.
/// <see cref="TimeSpanExt.Infinite"/> (= <see cref="TimeSpan.MaxValue"/>) means "never";
/// zero means "instantly".
/// </summary>
public sealed partial record RpcCallTimeouts
{
    public static TimeSpan DefaultDelayTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public static readonly RpcCallTimeouts None = new(); // Should go after DefaultDelayTimeout, coz it uses it!

    /// <summary>
    /// How long a call issued while the peer is not connected waits for the connection.
    /// On expiry the call fails with <see cref="RpcTimeoutException"/> of <see cref="RpcTimeoutKind.Connect"/> kind.
    /// </summary>
    public TimeSpan ConnectTimeout { get; init => field = value.AsTimeout(); }
    /// <summary>
    /// How long a sent call waits for its result while the peer is connected; the clock restarts
    /// when the call is resent on reconnect. On expiry the call fails with <see cref="RpcTimeoutException"/>
    /// of <see cref="RpcTimeoutKind.Run"/> kind.
    /// </summary>
    public TimeSpan RunTimeout { get; init => field = value.AsTimeout(); }
    /// <summary>
    /// How long a call that can fall back to a value it already has - currently a remote compute call
    /// with a cached value - waits for a disconnected peer before serving that value. The call stays
    /// pending to validate it once the peer is back, so it never fails here.
    /// Zero (the default) serves the cached value at once.
    /// </summary>
    public TimeSpan CacheFallbackDelay { get; init => field = value.AsTimeout(); }
    /// <summary>
    /// After how long a still-pending call is reported as delayed. What happens then is up to
    /// <see cref="RpcDelayedCallAction"/>: log (the default), abort with <see cref="RpcTimeoutException"/>
    /// of <see cref="RpcTimeoutKind.Delay"/> kind, or resend.
    /// </summary>
    public TimeSpan DelayTimeout { get; init => field = value.AsTimeout(); } = DefaultDelayTimeout;

    // TimeSpan overloads

    public RpcCallTimeouts()
        : this(TimeSpanExt.Infinite, TimeSpanExt.Infinite)
    { }

    public RpcCallTimeouts(TimeSpan runTimeout)
        : this(TimeSpanExt.Infinite, runTimeout)
    { }

    public RpcCallTimeouts(TimeSpan connectTimeout, TimeSpan runTimeout)
    {
        ConnectTimeout = connectTimeout;
        RunTimeout = runTimeout;
    }

    // TimeSpan? overloads

    public RpcCallTimeouts(TimeSpan? runTimeout)
        : this(TimeSpanExt.Infinite, runTimeout.AsTimeout())
    { }

    // double? overloads

    public RpcCallTimeouts(double? runTimeout)
        : this(TimeSpanExt.Infinite, runTimeout.AsTimeout())
    { }

    public RpcCallTimeouts(double? connectTimeout, double? runTimeout)
        : this(connectTimeout.AsTimeout(), runTimeout.AsTimeout())
    { }

    // Private methods

    private bool PrintMembers(StringBuilder sb)
    {
        sb.Append(nameof(ConnectTimeout)).Append(" = ").Append(ConnectTimeout.ToShortString());
        sb.Append(", ").Append(nameof(RunTimeout)).Append(" = ").Append(RunTimeout.ToShortString());
        sb.Append(", ").Append(nameof(CacheFallbackDelay)).Append(" = ").Append(CacheFallbackDelay.ToShortString());
        sb.Append(", ").Append(nameof(DelayTimeout)).Append(" = ").Append(DelayTimeout.ToShortString());
        return true;
    }
}
