namespace ActualLab.Rpc;

/// <summary>
/// Configuration options for the <see cref="RpcServiceRegistry"/>, including service and method factories.
/// </summary>
public record RpcRegistryOptions
{
    public static RpcRegistryOptions Default { get; set; } = new();

    // Caps RpcServiceRegistry's cache of per-VersionSet legacy method resolvers. The key is the
    // remote peer's handshake version set normalized to the scopes this registry uses, so the only
    // way past a handful of entries is a peer inventing versions for a scope that does exist.
    public int LegacyMethodResolverCacheCapacity { get; init; } = 256;

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
