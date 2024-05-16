namespace ActualLab.Fusion;

public static class ServiceProviderExt
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static StateFactory StateFactory(this IServiceProvider services)
        => services.GetRequiredService<StateFactory>();
}
