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
    // A sentinel resolved via Or(...) — it can't be None, which means "do nothing"
    Default = 0x100,
}

/// <summary>
/// Extension methods for <see cref="RpcDelayedCallAction"/>.
/// </summary>
public static class RpcDelayedCallActionExt
{
    public static RpcDelayedCallAction Or(this RpcDelayedCallAction action, RpcDelayedCallAction actionIfDefault)
        => action == RpcDelayedCallAction.Default ? actionIfDefault : action;
}
