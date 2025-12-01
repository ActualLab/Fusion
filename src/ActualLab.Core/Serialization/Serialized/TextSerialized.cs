using MessagePack;

namespace ActualLab.Serialization;

public static class TextSerialized
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TextSerialized<TValue> New<TValue>(TValue value)
        => new() { Value = value };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TextSerialized<TValue> New<TValue>(string data)
        => new() { Data = data };
}

#if !NET5_0
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
#endif
[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
public partial class TextSerialized<T>
{
#if NET9_0_OR_GREATER
    // ReSharper disable once StaticMemberInGenericType
    protected static readonly Lock StaticLock = new();
#else
    // ReSharper disable once StaticMemberInGenericType
    protected static readonly object StaticLock = new();
#endif

    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore]
    public T Value { get; init; } = default!;

    [DataMember(Order = 0), MemoryPackOrder(0), Key(0)]
    public string Data {
        get => Serialize();
        init => Value = Deserialize(value);
    }

    // ToString

    public override string ToString()
        => $"{GetType().GetName()}({Value})";

    // Private & protected methods

    private string Serialize()
        => !typeof(T).IsValueType && ReferenceEquals(Value, null)
            ? ""
            : GetSerializer().Write(Value);

    private T Deserialize(string data)
        => data.IsNullOrEmpty()
            ? default!
            : GetSerializer().Read(data);

    protected virtual ITextSerializer<T> GetSerializer()
        => TextSerializer<T>.Default;
}
