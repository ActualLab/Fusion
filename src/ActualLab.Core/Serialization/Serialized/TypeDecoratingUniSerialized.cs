using System.Diagnostics.CodeAnalysis;
using ActualLab.Internal;
using ActualLab.IO;
using ActualLab.Serialization.Internal;
using MessagePack;

namespace ActualLab.Serialization;

public static class TypeDecoratingUniSerialized
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TypeDecoratingUniSerialized<TValue> New<TValue>(TValue value = default!)
        => new() { Value = value };
}

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
        [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
        get => SerializeText(Value, SerializerKind.SystemJson);
        [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
        init => Value = DeserializeText(value, SerializerKind.SystemJson);
    }

    [JsonIgnore, MemoryPackIgnore, IgnoreMember]
    public string NewtonsoftJson {
        [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
        get => SerializeText(Value, SerializerKind.NewtonsoftJson);
        [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
        init => Value = DeserializeText(value, SerializerKind.NewtonsoftJson);
    }

    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, IgnoreMember, MemoryPackOrder(0)]
    public byte[] MemoryPack {
        [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
        get => SerializeBytes(Value, SerializerKind.MemoryPack);
        [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
        init => Value = DeserializeBytes(value, SerializerKind.MemoryPack);
    }

    [JsonIgnore, Newtonsoft.Json.JsonIgnore, MemoryPackIgnore, DataMember(Order = 0), Key(0)]
    public MessagePackData MessagePack {
        [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
        get => SerializeBytes(Value, SerializerKind.MessagePack);
        [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
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

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    private static string SerializeText(T value, SerializerKind serializerKind)
    {
        var serializer = (ITextSerializer)serializerKind.GetDefaultTypeDecoratingSerializer();
        return serializer.Write(value);
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    private static byte[] SerializeBytes(T value, SerializerKind serializerKind)
    {
        var serializer = serializerKind.GetDefaultTypeDecoratingSerializer();
        ArrayPoolBuffer<byte>? buffer = null;
        try {
#if !NETSTANDARD2_0
            if (serializerKind != SerializerKind.MemoryPack) {
                buffer = serializer.Write(value);
                return buffer.WrittenSpan.ToArray();
            }

            using var stateSnapshot = MemoryPackSerializer.ResetWriterState();
#endif
            buffer = serializer.Write(value);
            return buffer.WrittenSpan.ToArray();
        }
        finally {
            buffer?.Dispose();
        }
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    private static T DeserializeText(string text, SerializerKind serializerKind)
    {
        var serializer = (ITextSerializer)serializerKind.GetDefaultTypeDecoratingSerializer();
        return serializer.Read<T>(text);
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    private static T DeserializeBytes(byte[] bytes, SerializerKind serializerKind)
    {
        var serializer = serializerKind.GetDefaultTypeDecoratingSerializer();
#if !NETSTANDARD2_0
        if (serializerKind != SerializerKind.MemoryPack)
            return serializer.Read<T>(bytes);

        using var stateSnapshot = MemoryPackSerializer.ResetReaderState();
#endif
        return serializer.Read<T>(bytes);
    }
}
