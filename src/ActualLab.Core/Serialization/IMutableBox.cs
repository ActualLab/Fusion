using MessagePack;

namespace ActualLab.Serialization;

/// <summary>
/// A mutable version of <see cref="IBox"/> that allows setting the value.
/// </summary>
public interface IMutableBox : IBox
{
    new object? UntypedValue { get; set; }
}

/// <summary>
/// A serializable mutable box that wraps a single value of type <typeparamref name="T"/>.
/// </summary>
[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
[method: JsonConstructor, MemoryPackConstructor, SerializationConstructor]
public sealed partial class MutableBox<T>(T value) : IMutableBox
{
    [DataMember(Order = 0), MemoryPackOrder(0), Key(0)]
    public T Value { get; set; } = value!;

    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore, IgnoreMember]
    public object? UntypedValue {
        get => Value;
        set => Value = (T?)value!;
    }

    public MutableBox() : this(default!)
    { }

    public override string ToString()
        => $"{GetType().GetName()}({Value})";
}

/// <summary>
/// Factory methods for creating <see cref="MutableBox{T}"/> instances.
/// </summary>
public static class MutableBox
{
    public static MutableBox<T> New<T>(T value) => new(value);
}
