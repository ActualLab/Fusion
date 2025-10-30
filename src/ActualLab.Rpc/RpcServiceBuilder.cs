using ActualLab.Internal;

namespace ActualLab.Rpc;

public sealed class RpcServiceBuilder
{
    public RpcBuilder Rpc { get; }
    public Type Type { get; }
    public string Name { get; private set; }
    public RpcServiceMode Mode { get; private set; }
    public ServiceResolver? ServerResolver { get; private set; }
    public Type? ClientType { get; private set; }

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
        if (Mode is RpcServiceMode.Default or RpcServiceMode.Local)
            throw Internal.Errors.UnspecifiedServiceMode(Type);

        if (Mode.IsAnyServer() != ServerResolver is not null)
            throw Internal.Errors.ServiceModeAndServerResolverMismatch(Type, Mode);
        if (Mode.IsAnyClient() != ClientType is not null)
            throw Internal.Errors.ServiceModeAndClientTypeMismatch(Type, Mode);

        // There is no way to set just ClientType w/o adding Mode.IsAnyClient(), see HasClient()
        if (ClientType is not null && !Type.IsAssignableFrom(ClientType))
            throw Errors.MustBeAssignableTo(ClientType, Type);
    }

    public RpcServiceBuilder HasName(string name)
    {
        Name = name;
        return  this;
    }

    public RpcServiceBuilder ResetMode()
    {
        Mode = RpcServiceMode.Default;
        ServerResolver = null;
        ClientType = null;
        return this;
    }

    // IsXxx (= ResetMode().HasXxx(...))

    public RpcServiceBuilder IsClient<TClient>()
        where TClient : class, IRpcService
        => IsClient(typeof(TClient));
    public RpcServiceBuilder IsClient(Type? clientType = null)
        => ResetMode().HasClient(clientType);

    public RpcServiceBuilder IsServer<TImplementation>()
        where TImplementation : class, IRpcService
        => IsServer(typeof(TImplementation));
    public RpcServiceBuilder IsServer(Type? implementationType = null)
        => ResetMode().HasServer(implementationType ?? Type);

    public RpcServiceBuilder IsDistributed<TImplementation>()
        where TImplementation : class, IRpcService
        => IsDistributed(typeof(TImplementation));
    public RpcServiceBuilder IsDistributed(Type? implementationType = null)
    {
        implementationType ??= Type;
        return ResetMode().HasServer(implementationType, RpcServiceMode.Distributed).HasClient(implementationType);
    }

    // HasXxx

    public RpcServiceBuilder HasClient(Type? clientType = null)
    {
        if (clientType is not null && !Type.IsAssignableFrom(clientType))
            throw Errors.MustBeAssignableTo(clientType, Type);

        // ReSharper disable once BitwiseOperatorOnEnumWithoutFlags
        Mode |= RpcServiceMode.Client;
        ClientType = clientType ?? Type;
        return this;
    }

    public RpcServiceBuilder HasServer<TImplementation>(RpcServiceMode mode = RpcServiceMode.Server)
        => HasServer(ServiceResolver.New(typeof(TImplementation)), mode);
    public RpcServiceBuilder HasServer(Type implementationType, RpcServiceMode mode = RpcServiceMode.Server)
        => HasServer(ServiceResolver.New(implementationType), mode);
    public RpcServiceBuilder HasServer(ServiceResolver serverResolver, RpcServiceMode mode = RpcServiceMode.Server)
    {
        if (!mode.IsAnyServer())
            throw new ArgumentOutOfRangeException(nameof(mode));
        if (!Type.IsAssignableFrom(serverResolver.Type))
            throw Errors.MustBeAssignableTo(serverResolver.Type, Type, nameof(serverResolver));

        // ReSharper disable once BitwiseOperatorOnEnumWithoutFlags
        Mode |= mode;
        ServerResolver = serverResolver;
        return this;
    }

    public RpcBuilder Remove()
    {
        Rpc.Configuration.Services.Remove(Type);
        return Rpc;
    }
}
