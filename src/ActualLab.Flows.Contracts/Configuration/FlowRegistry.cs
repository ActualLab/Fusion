using System.Collections.Frozen;

namespace ActualLab.Flows;

public class FlowRegistry
{
    public IReadOnlyDictionary<Symbol, Type> Flows { get; private set; }
    public IReadOnlyDictionary<Type, Symbol> FlowNameByType { get; private set; }

    public FlowRegistry(FlowConfiguration configuration)
    {
        var flows = configuration.Flows;
        Flows = flows.ToFrozenDictionary();
        FlowNameByType = flows.ToFrozenDictionary(kv => kv.Value, kv => kv.Key);
    }
}
