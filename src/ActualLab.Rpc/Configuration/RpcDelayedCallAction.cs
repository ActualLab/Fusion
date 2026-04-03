namespace ActualLab.Rpc;

/// <summary>
/// Flags controlling how delayed outbound RPC calls are handled.
/// </summary>
[Flags]
public enum RpcDelayedCallAction
{
    None = 0,
    Abort = 0x1,
    Resend = 0x2,
    Log = 0x10,
    LogAndAbort = Log | Abort,
    LogAndResend = Log | Resend,
}
