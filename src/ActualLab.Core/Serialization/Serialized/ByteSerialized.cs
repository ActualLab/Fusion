using MessagePack;

namespace ActualLab.Serialization;

/// <summary>
/// Factory methods for <see cref="ByteSerialized{T}"/>.
/// </summary>
public static class ByteSerialized
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ByteSerialized<TValue> New<TValue>(TValue value = default!)
        => new() { Value = value };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ByteSerialized<TValue> New<TValue>(byte[] data)
        => new() { Data = data };
}

/// <summary>
/// A wrapper that auto-serializes its <see cref="Value"/> to a byte array on access via <see cref="IByteSerializer"/>.
/// </summary>
#if !NET5_0
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
#endif
[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
public partial class ByteSerialized<T>
{
#if NET9_0_OR_GREATER
    // ReSharper disable once StaticMemberInGenericType
    protected static readonly Lock StaticLock = new();
#else
    // ReSharper disable once StaticMemberInGenericType
    protected static readonly object StaticLock = new();
#endif

    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore, IgnoreMember]
    public T Value { get; init; } = default!;

    [DataMember(Order = 0), MemoryPackOrder(0), Key(0)]
    public byte[] Data {
        get => Serialize();
        init => Value = Deserialize(value);
    }

    // ToString

    public override string ToString()
        => $"{GetType().GetName()}({Value})";

    // Private & protected methods

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

    private T Deserialize(byte[] data)
        => data.Length == 0
            ? default!
            : GetSerializer().Read(data, out _);

    protected virtual IByteSerializer<T> GetSerializer()
        => ByteSerializer<T>.Default;
}
