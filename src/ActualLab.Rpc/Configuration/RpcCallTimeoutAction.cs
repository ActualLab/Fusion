namespace ActualLab.Rpc;

public enum RpcCallTimeoutAction
{
    None = 0,
    Log = 1,
    Throw = 2,
    LogAndThrow = 3,
}
