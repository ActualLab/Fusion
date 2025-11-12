using ActualLab.CommandR.Internal;

namespace ActualLab.CommandR;

public static class ServiceProviderExt
{
    extension(IServiceProvider services)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ICommander Commander()
            => services.GetRequiredService<ICommander>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CommanderHub CommanderHub()
            => services.GetRequiredService<CommanderHub>();
    }
}
