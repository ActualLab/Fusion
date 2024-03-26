using System.Diagnostics.CodeAnalysis;
using ActualLab.Interception;
using ActualLab.Interception.Interceptors;

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
                var service = peer == null ? LocalService : Client;
                return rpcMethodDef.Invoker.Invoke(service, invocation.Arguments);
            };

        var chainIntercept = ClientInterceptor.ChainIntercept<T>(methodDef);
        return invocation => {
            var peer = CallRouter.Invoke(rpcMethodDef, invocation.Arguments);
            return peer == null
                ? rpcMethodDef.Invoker.Invoke(LocalService, invocation.Arguments)
                : chainIntercept.Invoke(invocation);
        };
    }

    // We don't need to decorate this method with any dynamic access attributes
    protected override MethodDef? CreateMethodDef(MethodInfo method, Invocation initialInvocation)
        => ServiceDef.Methods.FirstOrDefault(m => m.Method == method);
}
