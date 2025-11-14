using MessagePack;

namespace ActualLab.Caching;

/// <summary>
/// This type is used by <see cref="Caching.GenericInstanceCache"/> class to substitute void types.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)] // Important!
[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
[DataContract, MessagePackObject, MemoryPackable]
public partial record struct VoidSurrogate
{
    public static readonly object BoxedDefault = new VoidSurrogate(0);

    [DataMember(Order = 0), Key(0), MemoryPackOrder(0)]
    public readonly byte Value;

    [JsonConstructor, Newtonsoft.Json.JsonConstructor, SerializationConstructor]
    public VoidSurrogate(byte value) => Value = 0;
}
