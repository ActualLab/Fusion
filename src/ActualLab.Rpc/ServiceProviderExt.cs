namespace ActualLab.Rpc;

/// <summary>
/// Extension methods for <see cref="IServiceProvider"/> to resolve RPC services.
/// </summary>
public static class ServiceProviderExt
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RpcHub RpcHub(this IServiceProvider services)
        => services.GetRequiredService<RpcHub>();
}
