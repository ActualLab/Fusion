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
    public Dictionary<Symbol, Type> Flows { get; }

    [RequiresUnreferencedCode(UnreferencedCode.Reflection)]
    internal FlowBuilder(
        IServiceCollection services,
        Action<FlowBuilder>? configure)
    {
        Services = services;
        Fusion = services.AddFusion();
        var flows = services.FindInstance<Dictionary<Symbol, Type>>();
        if (flows != null) {
            // Already configured
            Flows = flows;
            configure?.Invoke(this);
            return;
        }

        Flows = flows = new(64);
        services.AddInstance(flows, addInFront: true);

        services.AddSingleton<ImmutableBimap<Symbol, Type>>(static c => {
            var flowDefs = c.GetRequiredService<Dictionary<Symbol, Type>>();
            return new ImmutableBimap<Symbol, Type>(flowDefs);
        });

        configure?.Invoke(this);
    }

    public FlowBuilder AddFlow<TFlow>(Symbol name = default)
        where TFlow : Flow
        => AddFlow(typeof(TFlow), name);
    public FlowBuilder AddFlow(Type flowType, Symbol name = default)
    {
        if (!typeof(Flow).IsAssignableFrom(flowType))
            throw Errors.MustBeAssignableTo<Flow>(flowType);

        if (name.IsEmpty)
            name = flowType.GetName();
        Flows.Add(name, flowType);
        return this;
    }
}
