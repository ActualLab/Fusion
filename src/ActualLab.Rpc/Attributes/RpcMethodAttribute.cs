namespace ActualLab.Rpc;

/// <summary>
/// Configures RPC method properties such as name, timeouts, and local execution mode.
/// </summary>
[AttributeUsage(AttributeTargets.Interface | AttributeTargets.Method, Inherited = false)]
public sealed class RpcMethodAttribute : Attribute
{
    /// <summary>
    /// Method name.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Connect timeout (in seconds) for outbound calls.
    /// <code>double.NaN</code> means "use default".
    /// </summary>
    public double ConnectTimeout { get; set; } = double.NaN;

    /// <summary>
    /// Run timeout (in seconds) for outbound calls.
    /// <code>double.NaN</code> means "use default".
    /// </summary>
    public double RunTimeout { get; set; } = double.NaN;

    /// <summary>
    /// Delay timeout (in seconds) for outbound calls — calls exceeding this are considered delayed.
    /// <code>double.NaN</code> means "use default".
    /// </summary>
    public double DelayTimeout { get; set; } = double.NaN;

    /// <summary>
    /// Action to take when an outbound call is detected as delayed.
    /// <c>null</c> means "use default" (<see cref="RpcDelayedCallAction.Log"/>).
    /// </summary>
    public RpcDelayedCallAction? DelayAction { get; set; }

    /// <summary>
    /// Shard routing mode.
    /// </summary>
    public RpcLocalExecutionMode LocalExecutionMode { get; set; } = RpcLocalExecutionMode.Default;

    /// <summary>
    /// Controls outbound call behavior for connection waiting, reconnection, and resending.
    /// Default is <see cref="RpcRemoteExecutionMode.Default"/> (all flags set).
    /// NoWait methods always use <c>0</c> regardless of this setting.
    /// Compute methods must use <see cref="RpcRemoteExecutionMode.Default"/>.
    /// </summary>
    public RpcRemoteExecutionMode RemoteExecutionMode { get; set; } = RpcRemoteExecutionMode.Default;
}
