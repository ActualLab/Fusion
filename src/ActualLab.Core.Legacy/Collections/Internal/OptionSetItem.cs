using MessagePack;

namespace ActualLab.Collections.Internal;

/// <summary>
/// A serializable key-data pair used as a storage element in option sets.
/// </summary>
[StructLayout(LayoutKind.Auto)]
[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
[DataContract, MessagePackObject]
[method: JsonConstructor, Newtonsoft.Json.JsonConstructor, SerializationConstructor]
public record struct OptionSetItem<TData>(
    [property: DataMember(Order = 0), Key(0)] string Key,
    [property: DataMember(Order = 0), Key(1)] TData Data
);
