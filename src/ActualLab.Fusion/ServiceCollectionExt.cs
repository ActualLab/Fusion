using System.Diagnostics.CodeAnalysis;
using ActualLab.Fusion.Internal;
using ActualLab.Rpc;

namespace ActualLab.Fusion;

public static class ServiceCollectionExt
{
    [RequiresUnreferencedCode(UnreferencedCode.Fusion)]
    public static FusionBuilder AddFusion(
        this IServiceCollection services,
        RpcServiceMode serviceMode = RpcServiceMode.Default,
        bool setDefaultServiceMode = false)
        => new(services, null, serviceMode, setDefaultServiceMode);

    [RequiresUnreferencedCode(UnreferencedCode.Fusion)]
    public static IServiceCollection AddFusion(
        this IServiceCollection services,
        Action<FusionBuilder> configure)
        => new FusionBuilder(services, configure, RpcServiceMode.Default, false).Services;

    [RequiresUnreferencedCode(UnreferencedCode.Fusion)]
    public static IServiceCollection AddFusion(
        this IServiceCollection services,
        RpcServiceMode serviceMode,
        Action<FusionBuilder> configure)
        => new FusionBuilder(services, configure, serviceMode, false).Services;

    [RequiresUnreferencedCode(UnreferencedCode.Fusion)]
    public static IServiceCollection AddFusion(
        this IServiceCollection services,
        RpcServiceMode serviceMode,
        bool setDefaultServiceMode,
        Action<FusionBuilder> configure)
        => new FusionBuilder(services, configure, serviceMode, setDefaultServiceMode).Services;
}
