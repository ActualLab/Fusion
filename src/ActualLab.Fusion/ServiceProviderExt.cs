using ActualLab.Fusion.Internal;

namespace ActualLab.Fusion;

public static class ServiceProviderExt
{
    extension(IServiceProvider services)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FusionHub FusionHub()
            => services.GetRequiredService<FusionHub>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StateFactory StateFactory()
            => services.GetRequiredService<StateFactory>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsScoped()
            => services.GetRequiredService<StateFactory>().IsScoped;
    }
}
