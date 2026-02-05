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
    /// Log timeout (in seconds) for outbound calls.
    /// <code>double.NaN</code> means "use default".
    /// </summary>
    public double LogTimeout { get; set; } = double.NaN;

    /// <summary>
    /// Shard routing mode.
    /// </summary>
    public RpcLocalExecutionMode LocalExecutionMode { get; set; } = RpcLocalExecutionMode.Default;
}
