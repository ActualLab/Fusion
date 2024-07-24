using System.Diagnostics.CodeAnalysis;
using ActualLab.Fusion.Internal;
using ActualLab.Rpc;

namespace ActualLab.Fusion;

public static class ServiceCollectionExt
{
    [RequiresUnreferencedCode(UnreferencedCode.Fusion)]
    public static FusionBuilder AddFusion(
        this IServiceCollection services,
        RpcServiceMode defaultServiceMode = RpcServiceMode.Default,
        bool setDefaultServiceMode = false)
        => new(services, null, defaultServiceMode, setDefaultServiceMode);

    [RequiresUnreferencedCode(UnreferencedCode.Fusion)]
    public static IServiceCollection AddFusion(
        this IServiceCollection services,
        Action<FusionBuilder> configure)
        => new FusionBuilder(services, configure, RpcServiceMode.Default, false).Services;

    [RequiresUnreferencedCode(UnreferencedCode.Fusion)]
    public static IServiceCollection AddFusion(
        this IServiceCollection services,
        RpcServiceMode defaultServiceMode,
        Action<FusionBuilder> configure)
        => new FusionBuilder(services, configure, defaultServiceMode, false).Services;

    [RequiresUnreferencedCode(UnreferencedCode.Fusion)]
    public static IServiceCollection AddFusion(
        this IServiceCollection services,
        RpcServiceMode defaultServiceMode,
        bool setDefaultServiceMode,
        Action<FusionBuilder> configure)
        => new FusionBuilder(services, configure, defaultServiceMode, setDefaultServiceMode).Services;
}
