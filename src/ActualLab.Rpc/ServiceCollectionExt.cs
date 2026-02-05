namespace ActualLab.Rpc;

/// <summary>
/// Extension methods for <see cref="IServiceCollection"/> to register RPC services.
/// </summary>
public static class ServiceCollectionExt
{
    public static RpcBuilder AddRpc(
        this IServiceCollection services,
        RpcServiceMode defaultServiceMode = RpcServiceMode.Default,
        bool setDefaultServiceMode = false)
        => new(services, null, defaultServiceMode, setDefaultServiceMode);

    public static IServiceCollection AddRpc(
        this IServiceCollection services,
        Action<RpcBuilder> configure)
        => new RpcBuilder(services, configure, RpcServiceMode.Default, false).Services;

    public static IServiceCollection AddRpc(
        this IServiceCollection services,
        RpcServiceMode defaultServiceMode,
        Action<RpcBuilder> configure)
        => new RpcBuilder(services, configure, defaultServiceMode, false).Services;

    public static IServiceCollection AddRpc(
        this IServiceCollection services,
        RpcServiceMode defaultServiceMode,
        bool setDefaultServiceMode,
        Action<RpcBuilder> configure)
        => new RpcBuilder(services, configure, defaultServiceMode, setDefaultServiceMode).Services;
}
