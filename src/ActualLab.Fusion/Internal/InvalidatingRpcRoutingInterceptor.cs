using ActualLab.Interception;
using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Fusion.Internal;

// We avoid composition here to simplify the code, so the interceptor is just a configuration wrapper
public class InvalidatingRpcRoutingInterceptor(RpcRoutingInterceptor interceptor)
    : RpcRoutingInterceptor(interceptor.Settings,
        interceptor.Services,
        interceptor.ServiceDef,
        interceptor.LocalTarget,
        interceptor.AssumeConnected)
{
    protected override Task<object?> InvokeWithRerouting(
        Invocation invocation,
        RpcMethodDef methodDef,
        RpcOutboundContext context,
        RpcOutboundCall? call,
        Func<Invocation, Task>? localCallAsyncInvoker)
    {
        // If the call is invalidated, we should not reroute it
        return Invalidation.IsActive
            ? Task.FromResult<object?>(null)
            : base.InvokeWithRerouting(invocation, methodDef, context, call, localCallAsyncInvoker);
    }
}
