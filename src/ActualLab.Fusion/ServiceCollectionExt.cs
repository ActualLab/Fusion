using ActualLab.Rpc;

namespace ActualLab.Fusion;

/// <summary>
/// Extension methods for <see cref="IServiceCollection"/> to register Fusion services.
/// </summary>
public static class ServiceCollectionExt
{
    public static FusionBuilder AddFusion(
        this IServiceCollection services,
        RpcServiceMode defaultServiceMode = RpcServiceMode.Default,
        bool setDefaultServiceMode = false)
        => new(services, null, defaultServiceMode, setDefaultServiceMode);

    public static IServiceCollection AddFusion(
        this IServiceCollection services,
        Action<FusionBuilder> configure)
        => new FusionBuilder(services, configure, RpcServiceMode.Default, false).Services;

    public static IServiceCollection AddFusion(
        this IServiceCollection services,
        RpcServiceMode defaultServiceMode,
        Action<FusionBuilder> configure)
        => new FusionBuilder(services, configure, defaultServiceMode, false).Services;

    public static IServiceCollection AddFusion(
        this IServiceCollection services,
        RpcServiceMode defaultServiceMode,
        bool setDefaultServiceMode,
        Action<FusionBuilder> configure)
        => new FusionBuilder(services, configure, defaultServiceMode, setDefaultServiceMode).Services;
}
