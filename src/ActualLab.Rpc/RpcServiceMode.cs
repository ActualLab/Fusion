namespace ActualLab.Rpc;

public enum RpcServiceMode
{
    Default = 0,
    Local, // Singleton, no client or RPC exposure
    Client, // Client only, the implementation is ignored
    Server, // IService is an alias of Service; IService is exposed via RPC
    Distributed, // IService/Service is a single client+server instance, IService is exposed via RPC.
    DistributedPair, // IService is a client that may invoke local Server; IService is exposed via RPC
}

public static class RpcServiceModeExt
{
    public static RpcServiceMode Or(this RpcServiceMode mode, RpcServiceMode defaultMode)
        => mode == RpcServiceMode.Default ? defaultMode : mode;
}
