namespace ActualLab.Rpc;

/// <summary>
/// Identifies which timeout an <see cref="RpcTimeoutException"/> reports.
/// </summary>
public enum RpcTimeoutKind
{
    Unknown = 0, // E.g., when RpcTimeoutException rebuilt from ExceptionInfo on the other side
    Connect = 1,
    Run,
    Reconnect,
    Delay,
    Handshake,
    KeepAlive,
}
