using ActualLab.Internal;

namespace ActualLab.Flows;

public class FlowRegistryBuilder
{
    private readonly Dictionary<Symbol, Type> _flows = new(64);
    private readonly Dictionary<Type, Symbol> _flowNameByType = new(64);

    public IReadOnlyDictionary<Symbol, Type> Flows => _flows;

    public void Add(Type flowType, Symbol name = default)
    {
        Flow.RequireFlowType(flowType);
        if (_flowNameByType.ContainsKey(flowType))
            throw Errors.KeyAlreadyExists();

        if (name.IsEmpty)
            name = flowType.GetName();
        _flows.Add(name, flowType);
        _flowNameByType.Add(flowType, name);
    }
}
