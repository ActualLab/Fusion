using Stl.Interception;
using Stl.Interception.Interceptors;

namespace Stl.Rpc.Infrastructure;

public class RpcRoutingInterceptor : RpcInterceptorBase
{
    public new record Options : RpcInterceptorBase.Options
    {
        public static Options Default { get; set; } = new();
    }

    protected readonly RpcCallRouter RpcCallRouter;

    public object LocalService { get; private set; } = null!;
    public object RemoteService { get; private set; } = null!;

    public RpcRoutingInterceptor(Options settings, IServiceProvider services)
        : base(settings, services)
        => RpcCallRouter = Hub.CallRouter;

    public void Setup(RpcServiceDef serviceDef, object localService, object remoteService)
    {
        base.Setup(serviceDef);
        LocalService = localService;
        RemoteService = remoteService;
    }

    protected override Func<Invocation, object?> CreateHandler<T>(Invocation initialInvocation, MethodDef methodDef)
        => invocation => {
            var rpcMethodDef = (RpcMethodDef)methodDef;
            var peer = RpcCallRouter.Invoke(rpcMethodDef, invocation.Arguments);
            var service = peer == null ? LocalService : RemoteService;
            return rpcMethodDef.Invoker.Invoke(service, invocation.Arguments);
        };

    protected override MethodDef? CreateMethodDef(MethodInfo method, Invocation initialInvocation)
        => ServiceDef.Methods.FirstOrDefault(m => m.Method == method);
}
