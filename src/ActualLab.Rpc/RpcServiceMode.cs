namespace ActualLab.Rpc;

public enum RpcServiceMode
{
    Default = 0,
    Local,
    Server,
    Hybrid,
    HybridServer,
}

public static class RpcServiceModeExt
{
    public static RpcServiceMode Or(this RpcServiceMode mode, RpcServiceMode defaultMode)
        => mode == RpcServiceMode.Default ? defaultMode.OrNone() : mode;

    public static RpcServiceMode OrNone(this RpcServiceMode mode)
        => mode == RpcServiceMode.Default ? RpcServiceMode.Local : mode;
}
