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
        var chainIntercept = ClientInterceptor.GetChainInterceptFunc<T>(methodDef);
        return invocation => InvokeWithRerouting<T>(rpcMethodDef, invocation, chainIntercept);
    }

    // We don't need to decorate this method with any dynamic access attributes
    protected override MethodDef? CreateMethodDef(MethodInfo method, Type proxyType)
        => ServiceDef.Methods.FirstOrDefault(m => m.Method == method);

    // Private methods

    private async Task<T> InvokeWithRerouting<T>(
        RpcMethodDef methodDef, Invocation invocation, Func<Invocation, object?> chainIntercept)
    {
        while (true) {
            var peer = CallRouter.Invoke(methodDef, invocation.Arguments);
            try {
                var resultTask = peer == null
                    ? (Task<T>)methodDef.Invoker.Invoke(LocalService, invocation.Arguments)
                    : (Task<T>)chainIntercept.Invoke(invocation)!;
                return await resultTask.ConfigureAwait(false);
            }
            catch (RpcRerouteException) {
                // Restart to reroute
            }
        }
    }

}
