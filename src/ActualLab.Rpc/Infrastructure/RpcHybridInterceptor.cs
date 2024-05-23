using System.Diagnostics.CodeAnalysis;
using ActualLab.Interception;

namespace ActualLab.Rpc.Infrastructure;

public sealed class RpcHybridInterceptor : RpcInterceptorBase
{
    public new record Options : RpcInterceptorBase.Options
    {
        public static Options Default { get; set; } = new();
    }

    public readonly RpcCallRouter CallRouter;
    public readonly object LocalService;
    public readonly object Client;
    public readonly Interceptor ClientInterceptor;

    public RpcHybridInterceptor(
        Options settings,
        IServiceProvider services,
        RpcServiceDef serviceDef,
        object localService,
        object client)
        : base(settings, services, serviceDef)
    {
        CallRouter = Hub.CallRouter;
        LocalService = localService;
        Client = client;
        var clientProxy = (IProxy)client;
        ClientInterceptor = clientProxy.Interceptor;
        clientProxy.Interceptor = this;
    }

    protected override Func<Invocation, object?> CreateHandler<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>
        (Invocation initialInvocation, MethodDef methodDef)
    {
        var rpcMethodDef = (RpcMethodDef)methodDef;
        var chainInterceptFunc = ClientInterceptor.GetChainInterceptAsyncFunc<T>(methodDef);
        return invocation => {
            var peer = CallRouter.Invoke(rpcMethodDef, invocation.Arguments);
            var resultTask = peer.Ref.CanBeGone
                ? InvokeWithRerouting(rpcMethodDef, peer, chainInterceptFunc, invocation)
                : peer.ConnectionKind == RpcPeerConnectionKind.LocalCall
                    ? (Task<T>)methodDef.AsyncInvoker.Invoke(LocalService, invocation.Arguments)
                    : chainInterceptFunc.Invoke(invocation);
            return rpcMethodDef.UnwrapAsyncInvokerResult(resultTask);
        };
    }

    // We don't need to decorate this method with any dynamic access attributes
    protected override MethodDef? CreateMethodDef(MethodInfo method, Type proxyType)
        => ServiceDef.Methods.FirstOrDefault(m => m.Method == method);

    // Private methods

    private async Task<T> InvokeWithRerouting<T>(
        RpcMethodDef methodDef, RpcPeer peer, Func<Invocation, Task<T>> chainInterceptFunc, Invocation invocation)
    {
        while (true) {
            try {
                var resultTask = peer.ConnectionKind == RpcPeerConnectionKind.LocalCall
                    ? (Task<T>)methodDef.AsyncInvoker.Invoke(LocalService, invocation.Arguments)
                    : chainInterceptFunc.Invoke(invocation);
                return await resultTask.ConfigureAwait(false);
            }
            catch (RpcRerouteException) {
                peer = CallRouter.Invoke(methodDef, invocation.Arguments);
            }
        }
    }

}
