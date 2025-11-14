namespace ActualLab.Plugins;

public static class ServiceProviderExt
{
    extension(IServiceProvider services)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IPluginHost Plugins()
            => services.GetRequiredService<IPluginHost>();
    }
}
