namespace ActualLab.Rpc.Infrastructure;

public abstract class RpcOutboundMiddleware(IServiceProvider services)
    : RpcMiddleware(services)
{
    public abstract void OnPrepareCall(RpcOutboundContext context, bool isRerouted);
}
