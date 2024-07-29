namespace ActualLab.Rpc.Infrastructure;

public sealed class RpcOutboundMiddlewares(IServiceProvider services)
    : RpcMiddlewares<RpcOutboundMiddleware>(services)
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public RpcOutboundMiddlewares? NullIfEmpty()
        => HasInstances ? this : null;

    public void OnPrepareCall(RpcOutboundContext context, bool isRerouted)
    {
        foreach (var m in Instances)
            m.OnPrepareCall(context, isRerouted);
    }
}
