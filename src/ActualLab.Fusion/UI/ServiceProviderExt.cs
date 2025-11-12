namespace ActualLab.Fusion.UI;

public static class ServiceProviderExt
{
    extension(IServiceProvider services)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public UIActionTracker UIActionTracker()
            => services.GetRequiredService<UIActionTracker>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public UICommander UICommander()
            => services.GetRequiredService<UICommander>();
    }
}
