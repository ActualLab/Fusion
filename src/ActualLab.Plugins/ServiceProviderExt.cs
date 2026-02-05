namespace ActualLab.Plugins;

/// <summary>
/// Extension methods for <see cref="IServiceProvider"/> to access the <see cref="IPluginHost"/>.
/// </summary>
public static class ServiceProviderExt
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IPluginHost Plugins(this IServiceProvider services)
        => services.GetRequiredService<IPluginHost>();
}
