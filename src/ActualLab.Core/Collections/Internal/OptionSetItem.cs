using MessagePack;

namespace ActualLab.Collections.Internal;

[StructLayout(LayoutKind.Auto)]
[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
[method: JsonConstructor, Newtonsoft.Json.JsonConstructor, MemoryPackConstructor, SerializationConstructor]
public partial record struct OptionSetItem<TData>(
    [property: DataMember(Order = 0), MemoryPackOrder(0), Key(0)] string Key,
    [property: DataMember(Order = 0), MemoryPackOrder(1), Key(1)] TData Data
);
