using ActualLab.Fusion.Internal;

namespace ActualLab.Fusion;

/// <summary>
/// Extension methods for <see cref="IServiceProvider"/> to access Fusion services.
/// </summary>
public static class ServiceProviderExt
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FusionHub FusionHub(this IServiceProvider services)
        => services.GetRequiredService<FusionHub>();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static StateFactory StateFactory(this IServiceProvider services)
        => services.GetRequiredService<StateFactory>();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsScoped(this IServiceProvider services)
        => services.GetRequiredService<StateFactory>().IsScoped;
}
