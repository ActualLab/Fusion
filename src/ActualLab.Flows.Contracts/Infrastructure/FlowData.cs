namespace ActualLab.Flows.Infrastructure;

[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
[method: JsonConstructor, Newtonsoft.Json.JsonConstructor, MemoryPackConstructor]
// ReSharper disable once InconsistentNaming
public partial record struct FlowData(
    [property: DataMember(Order = 0), MemoryPackOrder(0)] long Version,
    [property: DataMember(Order = 1), MemoryPackOrder(1)] Symbol Step,
    [property: DataMember(Order = 2), MemoryPackOrder(2)] byte[]? Data
);
