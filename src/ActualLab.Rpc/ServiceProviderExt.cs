namespace ActualLab.Rpc;

public static class ServiceProviderExt
{
    extension(IServiceProvider services)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RpcHub RpcHub()
            => services.GetRequiredService<RpcHub>();
    }
}
