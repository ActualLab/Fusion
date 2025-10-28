using System.Diagnostics.CodeAnalysis;
using ActualLab.Interception;

namespace ActualLab.Rpc.Infrastructure;

public sealed class RpcSafeCallRouter(IServiceProvider services) : RpcServiceBase(services)
{
    [field: AllowNull, MaybeNull]
    public RpcCallRouter CallRouter {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => field ??= Services.GetRequiredService<RpcCallRouter>();
    }

    public RpcPeer Invoke(RpcMethodDef methodDef, ArgumentList arguments)
    {
        while (true) {
            try {
                var peerRef = CallRouter.Invoke(methodDef, arguments);
                return Hub.GetPeer(peerRef);
            }
            catch (RpcRerouteException e) {
                // This should never happen, but just in case...
                Log.LogWarning(e, "Rerouted while routing: {Method}{Arguments}", methodDef, arguments);
            }
        }
    }
}
