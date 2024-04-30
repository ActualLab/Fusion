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
    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore]
    public T Value { get; init; } = default!;

    [DataMember(Order = 0), MemoryPackOrder(0)]
    public byte[] Data {
        [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
        get => Serialize();
        [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
        init => Value = Deserialize(value);
    }

    // ToString

    public override string ToString()
        => $"{GetType().GetName()}({Value})";

    // Private & protected methods

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    private byte[] Serialize()
    {
        byte[] data;
        if (!typeof(T).IsValueType && ReferenceEquals(Value, null))
            data = [];
        else {
            using var bufferWriter = GetSerializer().Write(Value);
            data = bufferWriter.WrittenSpan.ToArray();
        }
        return data;
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    private T Deserialize(byte[] data)
        => data.Length == 0
            ? default!
            : GetSerializer().Read(data, out _);

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    protected virtual IByteSerializer<T> GetSerializer()
        => ByteSerializer<T>.Default;
}
