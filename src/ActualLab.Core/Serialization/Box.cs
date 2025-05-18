using MessagePack;

namespace ActualLab.Serialization;

public interface IBox
{
    object? UntypedValue { get; }
}

[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
[method: JsonConstructor, MemoryPackConstructor, SerializationConstructor]
public sealed partial record Box<T>(
    [property: DataMember(Order = 0), MemoryPackOrder(0), Key(0)] T Value
    ) : IBox
{
    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore, IgnoreMember]
    public object? UntypedValue => Value;

    public Box() : this(default(T)!)
    { }

    public override string ToString()
        => $"{GetType().GetName()}({Value})";
}

public static class Box
{
    public static Box<T> New<T>(T value) => new(value);
}
