using System.Diagnostics.CodeAnalysis;
using ActualLab.Internal;

namespace ActualLab.Serialization;

public static class ByteSerialized
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ByteSerialized<TValue> New<TValue>(TValue value = default!)
        => new() { Value = value };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public static ByteSerialized<TValue> New<TValue>(byte[] data)
        => new() { Data = data };
}

#if !NET5_0
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
#endif
[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
public partial class ByteSerialized<T>
{
    private byte[]? _data;

    [JsonIgnore, Newtonsoft.Json.JsonIgnore, MemoryPackIgnore]
    public T Value { get; init; } = default!;

    [DataMember(Order = 0), MemoryPackOrder(0)]
    public byte[] Data {
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
    private byte[] Serialize()
    {
        byte[] serializedValue;
        if (!typeof(T).IsValueType && ReferenceEquals(Value, null))
            serializedValue = Array.Empty<byte>();
        else {
            using var bufferWriter = GetSerializer().Write(Value);
            serializedValue = bufferWriter.WrittenSpan.ToArray();
        }
        _data = serializedValue;
        return serializedValue;
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    private T Deserialize(byte[] data)
    {
        var value = data.Length == 0
            ? default!
            : GetSerializer().Read(data, out _);
        _data = data;
        return value;
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    protected virtual IByteSerializer<T> GetSerializer()
        => ByteSerializer<T>.Default;
}
