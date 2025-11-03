using System.Diagnostics.CodeAnalysis;
using ActualLab.Interception;
using ActualLab.Rpc;

namespace ActualLab.Fusion;

public class FusionRpcServiceBuilder : RpcServiceBuilder
{
    public FusionBuilder Fusion { get; }
    public CommanderBuilder Commander => Fusion.Commander;
    public bool MustAddCommandHandlers { get; private set; } = true;

    public FusionRpcServiceBuilder(FusionBuilder fusion, Type type)
        : this(fusion, typeof(IComputeService), type)
    { }

    protected FusionRpcServiceBuilder(FusionBuilder fusion, Type rootType, Type type)
        : base(fusion.Rpc, rootType, type)
    {
        Fusion = fusion;
    }

    // New builder-style methods

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FusionRpcServiceBuilder HasCommandHandlers(bool hasCommandHandlers = true)
    {
        MustAddCommandHandlers = hasCommandHandlers;
        return this;
    }

    // Inject

    [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "We assume RPC-related code is fully preserved")]
    public override void Inject()
    {
        var serviceType = Type;
        if (Mode is RpcServiceMode.Client) {
            Services.AddSingleton(serviceType, CreateClient);
            if (MustAddCommandHandlers)
                Commander.AddHandlers(serviceType);
            return;
        }

        // Any server
        var implementationType = ImplementationResolver?.Type!;
        switch (Mode) {
            case RpcServiceMode.Local:
                // Local services are skipped during RpcServiceRegistry construction
                Services.AddSingleton(serviceType, CreateComputeService);
                if (MustAddCommandHandlers)
                    Commander.AddHandlers(serviceType, implementationType);
                return; // No alias is needed here
            case RpcServiceMode.Server:
                Services.AddSingleton(implementationType, CreateComputeService);
                break;
            case RpcServiceMode.ServerAndClient:
                Services.AddSingleton(implementationType, CreateComputeService);
                Services.AddSingleton(Proxies.GetProxyType(serviceType), CreateClient);
                break;
            case RpcServiceMode.Distributed:
                Services.AddSingleton(implementationType, CreateDistributedService);
                break;
            default:
                throw ActualLab.Rpc.Internal.Errors.UnspecifiedServiceMode(serviceType, Mode);
        }
        if (MustAddCommandHandlers)
            Commander.AddHandlers(serviceType);
        if (serviceType != implementationType)
            Services.AddAlias(serviceType, implementationType);
        return;

        object CreateComputeService(IServiceProvider c)
            => c.FusionHub().NewComputeServiceProxy(c, implementationType);

        object CreateClient(IServiceProvider c)
            => c.FusionHub().NewRemoteComputeServiceProxy(serviceType, serviceType);

        object CreateDistributedService(IServiceProvider c)
            => c.FusionHub().NewRemoteComputeServiceProxy(serviceType, implementationType);
    }

    // Builder-style methods from RpcServiceBuilder with "rewritten" return type

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public new FusionRpcServiceBuilder HasName(string name)
    {
        base.HasName(name);
        return this;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public new FusionRpcServiceBuilder ResetMode()
    {
        base.ResetMode();
        return this;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public new FusionRpcServiceBuilder IsClient()
    {
        base.IsClient();
        return this;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public new FusionRpcServiceBuilder IsLocal(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type implementationType)
    {
        base.IsLocal(implementationType);
        return this;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public new FusionRpcServiceBuilder IsLocal(ServiceResolver serviceResolver)
    {
        base.IsLocal(serviceResolver);
        return this;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public new FusionRpcServiceBuilder IsServer(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type implementationType)
    {
        base.IsServer(implementationType);
        return this;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public new FusionRpcServiceBuilder IsServer(ServiceResolver serviceResolver)
    {
        base.IsServer(serviceResolver);
        return this;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public new FusionRpcServiceBuilder IsDistributed(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type implementationType)
    {
        base.IsDistributed(implementationType);
        return this;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public new FusionRpcServiceBuilder HasClient()
    {
        base.HasClient();
        return this;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public new FusionRpcServiceBuilder HasServer(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type implementationType,
        RpcServiceMode mode = RpcServiceMode.Server)
    {
        base.HasServer(implementationType, mode);
        return this;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public new FusionRpcServiceBuilder HasServer(
        ServiceResolver serviceResolver,
        RpcServiceMode mode = RpcServiceMode.Server)
    {
        base.HasServer(serviceResolver, mode);
        return this;
    }
}
