using System.Collections.Frozen;

namespace ActualLab.Flows;

public class FlowRegistry : IHasServices
{

    public IServiceProvider Services { get; }
    public IReadOnlyDictionary<Symbol, Type> Types { get; private set; }
    public IReadOnlyDictionary<Type, Symbol> Names { get; private set; }

    public FlowRegistry(IServiceProvider services)
    {
        Services = services;
        var flowRegistryBuilder = services.GetRequiredService<FlowRegistryBuilder>();
        var flows = flowRegistryBuilder.Flows;
        Types = flows.ToFrozenDictionary();
        Names = flows.ToFrozenDictionary(kv => kv.Value, kv => kv.Key);
    }

    public Flow Create(Symbol flowName)
        => Create(Types[flowName]);
    public virtual Flow Create(Type flowType)
    {
        Flow.RequireCorrectType(flowType);
        return (Flow)flowType.CreateInstance();
    }
}
