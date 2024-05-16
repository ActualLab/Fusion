#if NET8_0_OR_GREATER
using System.Collections.Frozen;
#endif

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
#if NET8_0_OR_GREATER
        Types = flows.ToFrozenDictionary();
        Names = flows.ToFrozenDictionary(kv => kv.Value, kv => kv.Key);
#else
        Types = flows.ToDictionary(kv => kv.Key, kv => kv.Value);
        Names = flows.ToDictionary(kv => kv.Value, kv => kv.Key);
#endif
    }

    public FlowId NewId<TFlow>(string arguments)
        where TFlow : Flow
        => new(Names[typeof(TFlow)], arguments);

    public FlowId NewId(Type flowType, string arguments)
        => new(Names[flowType], arguments);
}
