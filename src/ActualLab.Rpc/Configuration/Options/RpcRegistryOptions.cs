namespace ActualLab.Rpc;

public class RpcRegistryOptions
{
    public static RpcRegistryOptions Default { get; set; } = new();

    public virtual RpcServiceDef CreateServiceDef(RpcHub hub, RpcServiceBuilder service)
        => new(hub, service);

    public virtual RpcMethodDef CreateMethodDef(RpcServiceDef serviceDef, MethodInfo methodInfo)
        => new(serviceDef, methodInfo);

    public virtual string GetServiceScope(RpcServiceDef serviceDef)
        => serviceDef.IsBackend
            ? RpcDefaults.BackendScope
            : RpcDefaults.ApiScope;
}
