using System.Diagnostics.CodeAnalysis;
using ActualLab.Interception;
using ActualLab.Interception.Interceptors;

namespace ActualLab.Rpc.Infrastructure;

public class RpcHybridInterceptor : RpcInterceptorBase
{
    public new record Options : RpcInterceptorBase.Options
    {
        public static Options Default { get; set; } = new();
    }

    protected readonly RpcCallRouter RpcCallRouter;

    public object LocalService { get; private set; } = null!;
    public object Client { get; private set; } = null!;
    public Interceptor? ClientInterceptor { get; private set; }

    public RpcHybridInterceptor(Options settings, IServiceProvider services)
        : base(settings, services)
        => RpcCallRouter = Hub.CallRouter;

    public void Setup(RpcServiceDef serviceDef, object localService, object client, bool reuseClientProxy = false)
    {
        base.Setup(serviceDef);
        LocalService = localService;
        Client = client;
        if (reuseClientProxy) {
            var clientProxy = (IProxy)client;
            ClientInterceptor = clientProxy.Interceptor;
            clientProxy.SetInterceptor(this);
        }
    }

    protected override Func<Invocation, object?> CreateHandler<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>
        (Invocation initialInvocation, MethodDef methodDef)
    {
        if (ClientInterceptor == null)
            return invocation => {
                var rpcMethodDef = (RpcMethodDef)methodDef;
                var peer = RpcCallRouter.Invoke(rpcMethodDef, invocation.Arguments);
                var service = peer == null ? LocalService : Client;
                return rpcMethodDef.Invoker.Invoke(service, invocation.Arguments);
            };

        Func<Invocation, T> clientIntercept = ClientInterceptor.Intercept<T>;
        return invocation => {
            var rpcMethodDef = (RpcMethodDef)methodDef;
            var peer = RpcCallRouter.Invoke(rpcMethodDef, invocation.Arguments);
            return peer == null
                ? rpcMethodDef.Invoker.Invoke(LocalService, invocation.Arguments)
                : clientIntercept.Invoke(invocation);
        };
    }

    // We don't need to decorate this method with any dynamic access attributes
    protected override MethodDef? CreateMethodDef(MethodInfo method, Invocation initialInvocation)
        => ServiceDef.Methods.FirstOrDefault(m => m.Method == method);
}
