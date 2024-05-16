namespace ActualLab.Flows;

[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
[method: JsonConstructor, Newtonsoft.Json.JsonConstructor, MemoryPackConstructor]
public partial record FlowTimerEvent(
    [property: DataMember(Order = 0), MemoryPackOrder(0)] Symbol Uuid,
    [property: DataMember(Order = 1), MemoryPackOrder(1)] FlowId FlowId,
    [property: DataMember(Order = 2), MemoryPackOrder(2)] string? Tag = null
) : IFlowEvent, IHasUuid;
