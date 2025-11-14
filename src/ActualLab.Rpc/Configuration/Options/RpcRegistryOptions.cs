namespace ActualLab.Rpc;

public record RpcRegistryOptions
{
    public static RpcRegistryOptions Default { get; set; } = new();

    // Delegate options
    public Func<RpcHub, RpcServiceBuilder, RpcServiceDef> ServiceDefFactory { get; init; }
    public Func<RpcServiceDef, MethodInfo, RpcMethodDef> MethodDefFactory { get; init; }
    public Func<RpcServiceDef, string> ServiceScopeResolver { get; init; }

    // ReSharper disable once ConvertConstructorToMemberInitializers
    public RpcRegistryOptions()
    {
        ServiceDefFactory = DefaultServiceDefFactory;
        MethodDefFactory = DefaultMethodDefFactory;
        ServiceScopeResolver = DefaultServiceScopeResolver;
    }

    // Protected methods

    protected static RpcServiceDef DefaultServiceDefFactory(RpcHub hub, RpcServiceBuilder service)
        => new(hub, service);

    protected static RpcMethodDef DefaultMethodDefFactory(RpcServiceDef serviceDef, MethodInfo methodInfo)
        => new(serviceDef, methodInfo);

    protected static string DefaultServiceScopeResolver(RpcServiceDef serviceDef)
        => serviceDef.IsBackend
            ? RpcDefaults.BackendScope
            : RpcDefaults.ApiScope;
}
