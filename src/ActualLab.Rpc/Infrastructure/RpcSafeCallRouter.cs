using ActualLab.Interception;

namespace ActualLab.Rpc.Infrastructure;

public sealed class RpcSafeCallRouter(IServiceProvider services) : RpcServiceBase(services)
{
    private RpcCallRouter? _callRouter;

    public RpcCallRouter CallRouter {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _callRouter ??= Services.GetRequiredService<RpcCallRouter>();
    }

    public RpcPeer Invoke(RpcMethodDef methodDef, ArgumentList arguments)
    {
        while (true) {
            try {
                var peerRef = CallRouter.Invoke(methodDef, arguments);
                return Hub.GetPeer(peerRef); // May throw RpcRerouteException!
            }
            catch (RpcRerouteException e) {
                Log.LogWarning(e, "Rerouting is requested during call routing: {Method}{Arguments}",
                    methodDef, arguments);
                // Thread.Yield();
            }
        }
    }
}
