namespace ActualLab.Rpc;

public enum RpcServiceMode
{
    Default = 0,
    Local, // Singleton, no client or RPC exposure
    Client, // Client only, the implementation is ignored
    Server, // IService -> Service; IService is exposed via RPC
    ServerAndClient, // IService is a client invoking a local Server when possible; IService is exposed via RPC
    Hybrid, // Service is a client+server, IService is exposed via RPC.
}

public static class RpcServiceModeExt
{
    public static RpcServiceMode Or(this RpcServiceMode mode, RpcServiceMode defaultMode)
        => mode == RpcServiceMode.Default ? defaultMode : mode;
}
