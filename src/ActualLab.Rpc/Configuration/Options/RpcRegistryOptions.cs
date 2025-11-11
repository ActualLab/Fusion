using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc;

public class RpcRegistryOptions(IServiceProvider services) : RpcServiceBase(services)
{
    public virtual RpcServiceDef CreateServiceDef(RpcServiceBuilder service)
        => new(Hub, service);

    public virtual RpcMethodDef CreateMethodDef(RpcServiceDef serviceDef, MethodInfo methodInfo)
        => new(serviceDef, methodInfo);

    public virtual string GetServiceScope(RpcServiceDef serviceDef)
        => serviceDef.IsBackend
            ? RpcDefaults.BackendScope
            : RpcDefaults.ApiScope;
}
