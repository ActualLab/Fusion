namespace ActualLab.Fusion.UI;

/// <summary>
/// Extension methods for <see cref="IServiceProvider"/> to resolve UI-related Fusion services.
/// </summary>
public static class ServiceProviderExt
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static UIActionTracker UIActionTracker(this IServiceProvider services)
        => services.GetRequiredService<UIActionTracker>();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static UICommander UICommander(this IServiceProvider services)
        => services.GetRequiredService<UICommander>();
}
