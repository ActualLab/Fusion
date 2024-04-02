namespace ActualLab.Collections.Internal;

[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
[method: JsonConstructor, Newtonsoft.Json.JsonConstructor, MemoryPackConstructor]
public partial record struct OptionSetItem<TData>(
    [property: DataMember(Order = 0), MemoryPackOrder(0)] string Key,
    [property: DataMember(Order = 0), MemoryPackOrder(1)] TData Data
);
