using ActualLab.CommandR.Internal;

namespace ActualLab.CommandR;

public static class ServiceProviderExt
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ICommander Commander(this IServiceProvider services)
        => services.GetRequiredService<ICommander>();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CommanderHub CommanderHub(this IServiceProvider services)
        => services.GetRequiredService<CommanderHub>();

    public static bool IsScoped(this IServiceProvider services)
    {
        services = services.GetRequiredService<IServiceProvider>();
        return !ReferenceEquals(services.Commander().Services, services);
    }
}
