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
    public readonly Interceptor? ClientInterceptor;

    public RpcHybridInterceptor(
        Options settings,
        IServiceProvider services,
        RpcServiceDef serviceDef,
        object localService,
        object client,
        bool reuseClientProxy = false)
        : base(settings, services, serviceDef)
    {
        CallRouter = Hub.CallRouter;
        LocalService = localService;
        Client = client;
        if (reuseClientProxy) {
            var clientProxy = (IProxy)client;
            ClientInterceptor = clientProxy.Interceptor;
            clientProxy.Interceptor = this;
        }
    }

    protected override Func<Invocation, object?> CreateHandler<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>
        (Invocation initialInvocation, MethodDef methodDef)
    {
        var rpcMethodDef = (RpcMethodDef)methodDef;
        if (ClientInterceptor == null)
            return invocation => {
                var peer = CallRouter.Invoke(rpcMethodDef, invocation.Arguments);
                if (peer == null)
                    return rpcMethodDef.Invoker.Invoke(LocalService, invocation.Arguments);
                if (!peer.Ref.CanBecomeObsolete)
                    return rpcMethodDef.Invoker.Invoke(Client, invocation.Arguments);
                return InvokeWithRerouting<T>(rpcMethodDef, invocation);
            };

        var chainIntercept = ClientInterceptor.GetChainInterceptFunc<T>(methodDef);
        return invocation => {
            var peer = CallRouter.Invoke(rpcMethodDef, invocation.Arguments);
            if (peer == null)
                return rpcMethodDef.Invoker.Invoke(LocalService, invocation.Arguments);
            if (!peer.Ref.CanBecomeObsolete)
                return chainIntercept.Invoke(invocation);
            return InvokeWithRerouting<T>(rpcMethodDef, invocation, chainIntercept);
        };
    }

    // We don't need to decorate this method with any dynamic access attributes
    protected override MethodDef? CreateMethodDef(MethodInfo method, Type proxyType)
        => ServiceDef.Methods.FirstOrDefault(m => m.Method == method);

    // Private methods

    private async Task<T> InvokeWithRerouting<T>(RpcMethodDef methodDef, Invocation invocation)
    {
        while (true) {
            try {
                var resultTask = (Task<T>)methodDef.Invoker.Invoke(Client, invocation.Arguments);
                return await resultTask.ConfigureAwait(false);
            }
            catch (RpcRerouteException) {
                var peer1 = CallRouter.Invoke(methodDef, invocation.Arguments);
                if (peer1 != null)
                    continue;

                var resultTask = (Task<T>)methodDef.Invoker.Invoke(LocalService, invocation.Arguments);
                return await resultTask.ConfigureAwait(false);
            }
        }
    }

    private async Task<T> InvokeWithRerouting<T>(RpcMethodDef methodDef, Invocation invocation, Func<Invocation, object?> chainIntercept)
    {
        while (true) {
            while (true) {
                try {
                    var resultTask = (Task<T>)chainIntercept.Invoke(invocation)!;
                    return await resultTask.ConfigureAwait(false);
                }
                catch (RpcRerouteException) {
                    var peer1 = CallRouter.Invoke(methodDef, invocation.Arguments);
                    if (peer1 != null)
                        continue;

                    var resultTask = (Task<T>)methodDef.Invoker.Invoke(LocalService, invocation.Arguments);
                    return await resultTask.ConfigureAwait(false);
                }
            }
        }
    }

}
