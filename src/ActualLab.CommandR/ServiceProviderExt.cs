using ActualLab.CommandR.Internal;

namespace ActualLab.CommandR;

/// <summary>
/// Extension methods for <see cref="IServiceProvider"/> to resolve commander services.
/// </summary>
public static class ServiceProviderExt
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ICommander Commander(this IServiceProvider services)
        => services.GetRequiredService<ICommander>();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CommanderHub CommanderHub(this IServiceProvider services)
        => services.GetRequiredService<CommanderHub>();
}
