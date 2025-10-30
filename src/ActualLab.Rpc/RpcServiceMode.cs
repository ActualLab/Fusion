namespace ActualLab.Rpc;

#pragma warning disable RCS1130

public enum RpcServiceMode
{
    /// <summary>
    /// Use <see cref="RpcBuilder.DefaultServiceMode"/>, or
    /// <c>FusionBuilder.DefaultServiceMode</c> in case with <c>FusionBuilder</c>.
    /// </summary>
    Default = 0,

    /// <summary>
    /// The service isn't going to be added to the <see cref="RpcServiceRegistry"/>.
    /// This mode can be used only during the registration.
    /// You can't see a service with this mode in the <see cref="RpcServiceRegistry"/>,
    /// i.e. <see cref="RpcServiceDef.Mode"/> can't be <see cref="RpcServiceMode.Local"/>.
    /// </summary>
    Local = 0x1,

    /// <summary>
    /// The service is exposed via RPC, so it can be called by remote peers.
    /// </summary>
    Server = 0x2,

    /// <summary>
    /// The service isn't exposed via RPC but is known to be an RPC client.
    /// </summary>
    Client = 0x4,

    /// <summary>
    /// <c>Service</c> is a singleton routing proxy extending the implementation;
    /// it routes calls to either the implementation (base method) or to the RPC client.
    /// It is exposed via RPC as <c>IService</c>, so it can be called both locally or remotely,
    /// and in all these cases the calls are going to be properly (re)routed.
    /// </summary>
    Distributed = Server | Client | 0x10,

    /// <summary>
    /// The service is exposed via RPC, so it can be called by remote peers,
    /// and there is also an RPC client for this service.
    /// You shouldn't use this mode (maybe except in tests).
    /// </summary>
    ServerAndClient = Server | Client,
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
}
