namespace ActualLab.Flows;

[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
[method: JsonConstructor, Newtonsoft.Json.JsonConstructor, MemoryPackConstructor]
public partial record FlowStartEvent(
    [property: DataMember(Order = 0), MemoryPackOrder(0)] FlowId FlowId
) : IFlowEvent;
