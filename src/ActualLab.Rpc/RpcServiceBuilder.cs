using System.Diagnostics.CodeAnalysis;
using ActualLab.Interception;
using ActualLab.Internal;

namespace ActualLab.Rpc;

public class RpcServiceBuilder
{
    public RpcBuilder Rpc { get; }
    public IServiceCollection Services => Rpc.Services;
    public Type Type { get; }
    public string Name { get; private set; }
    public RpcServiceMode Mode { get; private set; }
    public ServiceResolver? ImplementationResolver { get; private set; }
    public RpcLocalExecutionMode LocalExecutionMode { get; private set; }

    public Type? ClientType => Mode switch {
        RpcServiceMode.Client => Type,
        RpcServiceMode.Distributed => Type,
        RpcServiceMode.ServerAndClient => Proxies.GetProxyType(Type),
        _ => null,
    };

    public RpcServiceBuilder(RpcBuilder rpc, Type type)
        : this(rpc, typeof(IRpcService), type)
    { }

    protected RpcServiceBuilder(RpcBuilder rpc, Type rootType, Type type)
    {
        if (type.IsValueType)
            throw Errors.MustBeClass(type, nameof(type));
        if (!rootType.IsAssignableFrom(type))
            throw Errors.MustBeAssignableTo(type, rootType, nameof(type));

        Rpc = rpc;
        Type = type;
        Name = "";
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public RpcServiceBuilder HasName(string name)
    {
        Name = name;
        return  this;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public RpcServiceBuilder ResetMode()
    {
        Mode = RpcServiceMode.Default;
        ImplementationResolver = null;
        return this;
    }

    // IsXxx (= ResetMode().HasXxx(...))

    public RpcServiceBuilder IsClient()
        => ResetMode().HasClient();

    public RpcServiceBuilder IsLocal(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type implementationType)
        => ResetMode().HasServer(implementationType, RpcServiceMode.Local);

    public RpcServiceBuilder IsLocal(ServiceResolver serverResolver)
        => ResetMode().HasServer(serverResolver, RpcServiceMode.Local);

    public RpcServiceBuilder IsServer(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type implementationType)
        => ResetMode().HasServer(implementationType);

    public RpcServiceBuilder IsServer(ServiceResolver serverResolver)
        => ResetMode().HasServer(serverResolver);

    public RpcServiceBuilder IsDistributed(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type implementationType)
        => ResetMode().HasServer(implementationType, RpcServiceMode.Distributed).HasClient();

    public RpcServiceBuilder HasClient()
    {
        if (Mode is RpcServiceMode.Local)
            throw new InvalidOperationException("Cannot add Client mode to Local service.");

        // ReSharper disable once BitwiseOperatorOnEnumWithoutFlags
        Mode |= RpcServiceMode.Client;
        return this;
    }

    public RpcServiceBuilder HasServer(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type implementationType,
        RpcServiceMode mode = RpcServiceMode.Server)
        => HasServer(ServiceResolver.New(implementationType), mode);

    public RpcServiceBuilder HasServer(
        ServiceResolver serverResolver,
        RpcServiceMode mode = RpcServiceMode.Server)
    {
        if (mode is not RpcServiceMode.Local && !mode.IsAnyServer())
            throw new ArgumentOutOfRangeException(nameof(mode));
        if (!Type.IsAssignableFrom(serverResolver.Type))
            throw Errors.MustBeAssignableTo(serverResolver.Type, Type, nameof(serverResolver));

        // ReSharper disable once BitwiseOperatorOnEnumWithoutFlags
        Mode |= mode;
        ImplementationResolver = serverResolver;
        return this;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public RpcServiceBuilder HasLocalExecutionMode(RpcLocalExecutionMode mode)
    {
        LocalExecutionMode = mode;
        return  this;
    }

    public RpcBuilder Remove()
    {
        Rpc.Configuration.Services.Remove(Type);
        return Rpc;
    }

    // Inject

    [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "We assume RPC-related code is fully preserved")]
    public virtual void Inject()
    {
        var serviceType = Type;
        if (Mode is RpcServiceMode.Client) {
            Services.AddSingleton(serviceType, CreateClient);
            return;
        }

        // Any server
        var implementationResolver = ImplementationResolver?.Resolver;
        var implementationType = ImplementationResolver?.Type!;
        switch (Mode) {
            case RpcServiceMode.Local:
                // Local services are skipped during RpcServiceRegistry construction
                if (implementationResolver is null)
                    Services.AddSingleton(serviceType, implementationType);
                return; // No alias is needed here
            case RpcServiceMode.Server:
                if (implementationResolver is null)
                    Services.AddSingleton(implementationType);
                break;
            case RpcServiceMode.ServerAndClient:
                if (implementationResolver is null)
                    Services.AddSingleton(implementationType);
                Services.AddSingleton(Proxies.GetProxyType(serviceType), CreateClient);
                break;
            case RpcServiceMode.Distributed:
                if (implementationResolver is not null)
                    throw Internal.Errors.DistributedServicesMustNotHaveImplementationResolver();

                Services.AddSingleton(implementationType, CreateDistributedService);
                break;
            default:
                throw Internal.Errors.UnspecifiedServiceMode(serviceType, Mode);
        }
        if (serviceType != implementationType)
            Services.AddAlias(serviceType, implementationType);
        return;

        object CreateClient(IServiceProvider c)
            => c.RpcHub().InternalServices.NewProxy(serviceType, serviceType);

        object CreateDistributedService(IServiceProvider c)
            => c.RpcHub().InternalServices.NewProxy(serviceType, implementationType);
    }
}
