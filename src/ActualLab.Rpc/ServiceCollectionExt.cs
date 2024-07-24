using System.Diagnostics.CodeAnalysis;
using ActualLab.Rpc.Internal;

namespace ActualLab.Rpc;

public static class ServiceCollectionExt
{
    [RequiresUnreferencedCode(UnreferencedCode.Rpc)]
    public static RpcBuilder AddRpc(
        this IServiceCollection services,
        RpcServiceMode defaultServiceMode = RpcServiceMode.Default,
        bool setDefaultServiceMode = false)
        => new(services, null, defaultServiceMode, setDefaultServiceMode);

    [RequiresUnreferencedCode(UnreferencedCode.Rpc)]
    public static IServiceCollection AddRpc(
        this IServiceCollection services,
        Action<RpcBuilder> configure)
        => new RpcBuilder(services, configure, RpcServiceMode.Default, false).Services;

    [RequiresUnreferencedCode(UnreferencedCode.Rpc)]
    public static IServiceCollection AddRpc(
        this IServiceCollection services,
        RpcServiceMode defaultServiceMode,
        Action<RpcBuilder> configure)
        => new RpcBuilder(services, configure, defaultServiceMode, false).Services;

    [RequiresUnreferencedCode(UnreferencedCode.Rpc)]
    public static IServiceCollection AddRpc(
        this IServiceCollection services,
        RpcServiceMode defaultServiceMode,
        bool setDefaultServiceMode,
        Action<RpcBuilder> configure)
        => new RpcBuilder(services, configure, defaultServiceMode, setDefaultServiceMode).Services;
}
