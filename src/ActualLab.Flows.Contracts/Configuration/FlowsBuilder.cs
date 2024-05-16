using System.Diagnostics.CodeAnalysis;
using ActualLab.CommandR;
using ActualLab.Flows.Infrastructure;
using ActualLab.Fusion;
using ActualLab.Internal;
using ActualLab.Rpc;

namespace ActualLab.Flows;

public readonly struct FlowsBuilder
{
    public IServiceCollection Services { get; }
    public FusionBuilder Fusion { get; }
    public CommanderBuilder Commander => Fusion.Commander;
    public RpcBuilder Rpc => Fusion.Rpc;
    public FlowRegistryBuilder Flows { get; }

    [RequiresUnreferencedCode(UnreferencedCode.Reflection)]
    internal FlowsBuilder(
        IServiceCollection services,
        Action<FlowsBuilder>? configure)
    {
        Services = services;
        Fusion = services.AddFusion();
        var flows = services.FindInstance<FlowRegistryBuilder>();
        if (flows != null) {
            // Already configured
            Flows = flows;
            configure?.Invoke(this);
            return;
        }

        Flows = flows = new();
        services.AddInstance(flows, addInFront: true);

        // Core services
        services.AddSingleton(c => new FlowRegistry(c));
        services.AddSingleton(_ => new FlowSerializer());
        services.AddSingleton(c => new FlowHost(c));
        services.AddSingleton(c => new FlowEventForwarder(c));
        Commander.AddHandlers<FlowEventForwarder>();

        configure?.Invoke(this);
    }

    public FlowsBuilder Add<TFlow>(Symbol name = default)
        where TFlow : Flow
        => Add(typeof(TFlow), name);

    public FlowsBuilder Add(Type flowType, Symbol name = default)
    {
        Flows.Add(flowType, name);
        return this;
    }
}
