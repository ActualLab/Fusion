using ActualLab.IO;
using ActualLab.Serialization.Internal;
using MessagePack;

namespace ActualLab.Serialization;

/// <summary>
/// Factory methods for <see cref="TypeDecoratingUniSerialized{T}"/>.
/// </summary>
public static class TypeDecoratingUniSerialized
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TypeDecoratingUniSerialized<TValue> New<TValue>(TValue value = default!)
        => new() { Value = value };
}

/// <summary>
/// A type-decorating variant of <see cref="UniSerialized{T}"/> that prefixes serialized data
/// with type information across all supported serializer formats.
/// </summary>
#if !NET5_0
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
#endif
[StructLayout(LayoutKind.Auto)]
[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
public readonly partial struct TypeDecoratingUniSerialized<T>
{
    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore, IgnoreMember]
    public T Value { get; init; } = default!;

    [JsonInclude, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore, IgnoreMember]
    public string Json {
        get => SerializeText(Value, SerializerKind.SystemJson);
        init => Value = DeserializeText(value, SerializerKind.SystemJson);
    }

    [JsonIgnore, MemoryPackIgnore, IgnoreMember]
    public string NewtonsoftJson {
        get => SerializeText(Value, SerializerKind.NewtonsoftJson);
        init => Value = DeserializeText(value, SerializerKind.NewtonsoftJson);
    }

    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, IgnoreMember, MemoryPackOrder(0)]
    public byte[] MemoryPack {
        get => SerializeBytes(Value, SerializerKind.MemoryPack);
        init => Value = DeserializeBytes(value, SerializerKind.MemoryPack);
    }

    [JsonIgnore, Newtonsoft.Json.JsonIgnore, MemoryPackIgnore, DataMember(Order = 0), Key(0)]
    public MessagePackData MessagePack {
        get => SerializeBytes(Value, SerializerKind.MessagePack);
        init => Value = DeserializeBytes(value.Data, SerializerKind.MessagePack);
    }

    [MemoryPackConstructor]
    public TypeDecoratingUniSerialized(byte[] memoryPack)
        => MemoryPack = memoryPack;

    [SerializationConstructor]
    public TypeDecoratingUniSerialized(MessagePackData messagePack)
        => MessagePack = messagePack;

    // ToString

    public override string ToString()
        => $"{GetType().GetName()}({Value})";

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator TypeDecoratingUniSerialized<T>(T value) => new() { Value = value };

    // Private methods

    private static string SerializeText(T value, SerializerKind serializerKind)
    {
        var serializer = (ITextSerializer)serializerKind.GetDefaultTypeDecoratingSerializer();
        return serializer.Write(value);
    }

    private static byte[] SerializeBytes(T value, SerializerKind serializerKind)
    {
        var serializer = serializerKind.GetDefaultTypeDecoratingSerializer();
        ArrayPoolBuffer<byte>? buffer = null;
        try {
#if !NETSTANDARD2_0
            if (serializerKind != SerializerKind.MemoryPack) {
                buffer = serializer.Write(value, typeof(T));
                return buffer.WrittenSpan.ToArray();
            }

            using var stateSnapshot = MemoryPackSerializer.ResetWriterState();
#endif
            buffer = serializer.Write(value, typeof(T));
            return buffer.WrittenSpan.ToArray();
        }
        finally {
            buffer?.Dispose();
        }
    }

    private static T DeserializeText(string text, SerializerKind serializerKind)
    {
        var serializer = (ITextSerializer)serializerKind.GetDefaultTypeDecoratingSerializer();
        return serializer.Read<T>(text);
    }

    private static T DeserializeBytes(byte[] bytes, SerializerKind serializerKind)
    {
        var serializer = serializerKind.GetDefaultTypeDecoratingSerializer();
#if !NETSTANDARD2_0
        if (serializerKind != SerializerKind.MemoryPack)
            return (T)serializer.Read(bytes, typeof(T), out _)!;

        using var stateSnapshot = MemoryPackSerializer.ResetReaderState();
#endif
        return (T)serializer.Read(bytes, typeof(T), out _)!;
    }
}
