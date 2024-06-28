using ActualLab.Interception;

namespace ActualLab.Rpc.Infrastructure;

public sealed class RpcSafeCallRouter(IServiceProvider services)
{
    private ILogger<RpcSafeCallRouter>? _log;
    private ILogger Log => _log ??= services.GetRequiredService<ILogger<RpcSafeCallRouter>>();

    public readonly RpcCallRouter UnsafeCallRouter = services.GetRequiredService<RpcCallRouter>();

    public RpcPeer Invoke(RpcMethodDef methodDef, ArgumentList arguments)
    {
        while (true) {
            try {
                return UnsafeCallRouter.Invoke(methodDef, arguments);
            }
            catch (RpcRerouteException e) {
                Log.LogWarning(e, "Rerouting is requested during call routing: {Method}{Arguments}",
                    methodDef, arguments);
                // Thread.Yield();
            }
        }
    }
}
