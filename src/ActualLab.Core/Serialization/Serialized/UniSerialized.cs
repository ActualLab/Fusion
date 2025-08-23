using System.Diagnostics.CodeAnalysis;
using ActualLab.IO;
using ActualLab.Serialization.Internal;
using MessagePack;

namespace ActualLab.Serialization;

public static class UniSerialized
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static UniSerialized<TValue> New<TValue>(TValue value = default!)
        => new() { Value = value };
}

#if !NET5_0
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
#endif
[StructLayout(LayoutKind.Auto)]
[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
public readonly partial struct UniSerialized<T>
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
    public UniSerialized(byte[] memoryPack)
        => MemoryPack = memoryPack;

    [SerializationConstructor]
    public UniSerialized(MessagePackData messagePack)
        => MessagePack = messagePack;

    // ToString

    public override string ToString()
        => $"{GetType().GetName()}({Value})";

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator UniSerialized<T>(T value) => new() { Value = value };

    // Private methods

    private static string SerializeText(T value, SerializerKind serializerKind)
    {
        var serializer = (ITextSerializer)serializerKind.GetDefaultSerializer();
        return serializer.Write(value);
    }

    private static byte[] SerializeBytes(T value, SerializerKind serializerKind)
    {
        var serializer = serializerKind.GetDefaultSerializer();
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
        var serializer = (ITextSerializer)serializerKind.GetDefaultSerializer();
        return serializer.Read<T>(text);
    }

    private static T DeserializeBytes(byte[] bytes, SerializerKind serializerKind)
    {
        var serializer = serializerKind.GetDefaultSerializer();
#if !NETSTANDARD2_0
        if (serializerKind != SerializerKind.MemoryPack)
            return (T)serializer.Read(bytes, typeof(T), out _)!;

        using var stateSnapshot = MemoryPackSerializer.ResetReaderState();
#endif
        return (T)serializer.Read(bytes, typeof(T), out _)!;
    }
}
