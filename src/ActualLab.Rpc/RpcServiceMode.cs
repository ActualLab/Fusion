namespace ActualLab.Rpc;

public enum RpcServiceMode
{
    Default = 0,
    Local,
    Server,
    ServerAndRouter,
    Hybrid,
}

public static class RpcServiceModeExt
{
    public static RpcServiceMode Or(this RpcServiceMode mode, RpcServiceMode defaultMode)
        => mode == RpcServiceMode.Default ? defaultMode : mode;
}
