using ActualLab.Internal;

namespace ActualLab.Rpc;

public sealed class RpcServiceBuilder
{
    public RpcBuilder Rpc { get; }
    public Type Type { get; }
    public string Name { get; set; }
    public RpcServiceMode Mode { get; set; }
    public ServiceResolver? ServerResolver { get; private set; }

    public RpcServiceBuilder(RpcBuilder rpc, Type type, string name = "")
    {
        if (type.IsValueType)
            throw Errors.MustBeClass(type, nameof(type));

        Rpc = rpc;
        Type = type;
        Name = name;
    }

    public void Validate()
    {
        if (Mode == RpcServiceMode.Default)
            throw Internal.Errors.UnspecifiedServiceMode(Type);

        if (Mode.IsAnyServer()) {
            if (ServerResolver is null)
                throw Internal.Errors.UnspecifiedServerResolver(Type, Mode);
        }
        else {
            if (ServerResolver is not null)
                throw Internal.Errors.UnexpectedServerResolver(Type, Mode);
        }
    }

    public RpcServiceBuilder HasName(string name)
    {
        Name = name;
        return  this;
    }

    public RpcServiceBuilder IsClient()
    {
        Mode = RpcServiceMode.Client;
        ServerResolver = null;
        return this;
    }

    public RpcServiceBuilder IsServer<TImplementation>()
        where TImplementation : class, IRpcService
        => HasServer(typeof(TImplementation), RpcServiceMode.Server);
    public RpcServiceBuilder IsServer(Type? implementationType = null)
        => HasServer(implementationType ?? Type, RpcServiceMode.Server);

    public RpcServiceBuilder IsDistributed()
        => HasServer(Type, RpcServiceMode.Distributed);

    public RpcServiceBuilder IsDistributedPair<TImplementation>()
        where TImplementation : class, IRpcService
        => HasServer(typeof(TImplementation), RpcServiceMode.DistributedPair);
    public RpcServiceBuilder IsDistributedPair(Type implementationType)
        => HasServer(implementationType, RpcServiceMode.DistributedPair);

    public RpcServiceBuilder IsClientAndServer<TImplementation>()
        where TImplementation : class, IRpcService
        => HasServer(typeof(TImplementation), RpcServiceMode.ClientAndServer);
    public RpcServiceBuilder IsClientAndServer(Type? implementationType = null)
        => HasServer(implementationType ?? Type, RpcServiceMode.ClientAndServer);

    public RpcServiceBuilder HasServer<TImplementation>(RpcServiceMode mode)
        => HasServer(ServiceResolver.New(typeof(TImplementation)), mode);
    public RpcServiceBuilder HasServer(Type implementationType, RpcServiceMode mode)
        => HasServer(ServiceResolver.New(implementationType), mode);
    public RpcServiceBuilder HasServer(ServiceResolver serverResolver, RpcServiceMode mode)
    {
        if (!mode.IsAnyServer())
            throw new ArgumentOutOfRangeException(nameof(mode));
        if (!Type.IsAssignableFrom(serverResolver.Type))
            throw Errors.MustBeAssignableTo(serverResolver.Type, Type, nameof(serverResolver));

        Mode = mode;
        ServerResolver = serverResolver;
        return this;
    }

    public RpcBuilder Remove()
    {
        Rpc.Configuration.Services.Remove(Type);
        return Rpc;
    }
}
