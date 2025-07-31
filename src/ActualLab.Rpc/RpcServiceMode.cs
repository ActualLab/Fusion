namespace ActualLab.Rpc;

public enum RpcServiceMode
{
    /// <summary>
    /// Use <see cref="RpcBuilder.DefaultServiceMode"/>, or
    /// <c>FusionBuilder.DefaultServiceMode</c> in case with <c>FusionBuilder</c>.
    /// </summary>
    Default = 0,

    /// <summary>
    /// The service is a singleton.
    /// <c>IService</c> is unexposed via RPC (i.e., it cannot be called by remote peers).
    /// </summary>
    Local = 0x1,

    /// <summary>
    /// <c>IService</c> is an alias of <c>Service</c>.
    /// <c>IService</c> is exposed via RPC (i.e., it can be called by remote peers).
    /// </summary>
    Server = 0x2,

    /// <summary>
    /// <c>IService</c> is an RPC client.
    /// <c>IService</c> is unexposed via RPC (i.e., it cannot be called by remote peers).
    /// </summary>
    Client = 0x4,

    /// <summary>
    /// <c>IService</c> is a routing proxy extending <c>Service</c> that routes to either base method call or RPC client.
    /// <c>IService</c> is exposed via RPC (i.e., it can be called by remote peers).
    /// </summary>
    Distributed = Server | Client | 0x10,
    /// <summary>
    /// <c>IService</c> is a routing proxy that routes to either <c>Service</c> or RPC client.
    /// <c>IService</c> is exposed via RPC (i.e., it can be called by remote peers).
    /// </summary>
    DistributedPair = Distributed | 0x20,

    /// <summary>
    /// <c>IService</c> is an RPC client.
    /// <c>Service</c> is exposed via RPC as <c>IService</c> (i.e., it cannot be called by remote peers).
    /// </summary>
    ClientAndServer = Server | Client | 0x40,
}

public static class RpcServiceModeExt
{
    public static RpcServiceMode Or(this RpcServiceMode mode, RpcServiceMode defaultMode)
        => mode == RpcServiceMode.Default ? defaultMode : mode;

    public static bool IsAnyClient(this RpcServiceMode mode)
        // ReSharper disable once BitwiseOperatorOnEnumWithoutFlags
        => (mode & RpcServiceMode.Client) != 0;

    public static bool IsAnyServer(this RpcServiceMode mode)
        // ReSharper disable once BitwiseOperatorOnEnumWithoutFlags
        => (mode & RpcServiceMode.Server) != 0;

    public static bool IsAnyDistributed(this RpcServiceMode mode)
        // ReSharper disable once BitwiseOperatorOnEnumWithoutFlags
        => (mode & RpcServiceMode.Distributed) != 0;
}
