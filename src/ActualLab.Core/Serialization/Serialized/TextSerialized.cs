using System.Diagnostics.CodeAnalysis;
using ActualLab.Internal;

namespace ActualLab.Serialization;

public static class TextSerialized
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TextSerialized<TValue> New<TValue>(TValue value)
        => new() { Value = value };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public static TextSerialized<TValue> New<TValue>(string data)
        => new() { Data = data };
}

#if !NET5_0
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
#endif
[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
public partial class TextSerialized<T>
{
    private string? _data;

    [JsonIgnore, Newtonsoft.Json.JsonIgnore, MemoryPackIgnore]
    public T Value { get; init; } = default!;

    [DataMember(Order = 0), MemoryPackOrder(0)]
    public string Data {
        [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
        get => _data ??= Serialize();
        [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
        init => Value = Deserialize(value);
    }

    // ToString

    public override string ToString()
        => $"{GetType().GetName()}(...)";

    // Private & protected methods

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    private string Serialize()
        => !typeof(T).IsValueType && ReferenceEquals(Value, null)
            ? ""
            : GetSerializer().Write(Value);

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    private T Deserialize(string data)
    {
        var value = data.IsNullOrEmpty()
            ? default!
            : GetSerializer().Read(data);
        _data = data;
        return value;
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    protected virtual ITextSerializer<T> GetSerializer()
        => TextSerializer<T>.Default;
}
