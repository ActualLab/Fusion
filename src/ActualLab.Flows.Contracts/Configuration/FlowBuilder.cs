using System.Diagnostics.CodeAnalysis;
using ActualLab.CommandR;
using ActualLab.Fusion;
using ActualLab.Internal;
using ActualLab.Rpc;

namespace ActualLab.Flows;

public readonly struct FlowBuilder
{
    public IServiceCollection Services { get; }
    public FusionBuilder Fusion { get; }
    public CommanderBuilder Commander => Fusion.Commander;
    public RpcBuilder Rpc => Fusion.Rpc;
    public FlowConfiguration Flows { get; }

    [RequiresUnreferencedCode(UnreferencedCode.Reflection)]
    internal FlowBuilder(
        IServiceCollection services,
        Action<FlowBuilder>? configure)
    {
        Services = services;
        Fusion = services.AddFusion();
        var flows = services.FindInstance<FlowConfiguration>();
        if (flows != null) {
            // Already configured
            Flows = flows;
            configure?.Invoke(this);
            return;
        }

        Flows = flows = new();
        services.AddInstance(flows, addInFront: true);
        services.AddSingleton<FlowRegistry>();
        configure?.Invoke(this);
    }

    public FlowBuilder AddFlow<TFlow>(Symbol name = default)
        where TFlow : Flow
        => AddFlow(typeof(TFlow), name);

    public FlowBuilder AddFlow(Type flowType, Symbol name = default)
    {
        Flows.Add(flowType, name);
        return this;
    }
}
