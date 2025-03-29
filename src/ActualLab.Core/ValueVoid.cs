using MessagePack;

namespace ActualLab;

[StructLayout(LayoutKind.Sequential, Pack = 1)] // Important!
[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
[DataContract, MessagePackObject, MemoryPackable]
public partial record struct ValueVoid
{
    [DataMember(Order = 0), Key(0), MemoryPackOrder(0)]
    public readonly byte Value;

    [JsonConstructor, Newtonsoft.Json.JsonConstructor, SerializationConstructor]
    public ValueVoid(byte value) => Value = 0;
}
