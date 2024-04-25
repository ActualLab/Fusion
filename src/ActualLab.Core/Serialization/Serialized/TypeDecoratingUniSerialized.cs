using System.Diagnostics.CodeAnalysis;
using ActualLab.Internal;
using ActualLab.Serialization.Internal;

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
[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
public readonly partial struct TypeDecoratingUniSerialized<T>
{
    [JsonIgnore, Newtonsoft.Json.JsonIgnore, MemoryPackIgnore]
    public T Value { get; init; } = default!;

    [JsonInclude, Newtonsoft.Json.JsonIgnore, MemoryPackIgnore]
    public string Json {
        [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
        get => SerializeText(Value, SerializerKind.SystemJson);
        [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
        init => Value = DeserializeText(value, SerializerKind.SystemJson);
    }

    [JsonIgnore, MemoryPackIgnore]
    public string NewtonsoftJson {
        [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
        get => SerializeText(Value, SerializerKind.NewtonsoftJson);
        [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
        init => Value = DeserializeText(value, SerializerKind.NewtonsoftJson);
    }

    [JsonIgnore, Newtonsoft.Json.JsonIgnore, MemoryPackOrder(0)]
    public byte[] MemoryPack {
        [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
        get => SerializeBytes(Value, SerializerKind.MemoryPack);
        [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
        init => Value = DeserializeBytes(value, SerializerKind.MemoryPack);
    }

    [JsonIgnore, Newtonsoft.Json.JsonIgnore, MemoryPackIgnore, DataMember(Order = 0)]
    public MessagePackData MessagePack {
        [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
        get => SerializeBytes(Value, SerializerKind.MessagePack);
        [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
        init => Value = DeserializeBytes(value.Data, SerializerKind.MessagePack);
    }

    [MemoryPackConstructor]
    public TypeDecoratingUniSerialized(byte[] memoryPack)
        => MemoryPack = memoryPack;

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
        if (serializerKind != SerializerKind.MemoryPack) {
            using var buffer = serializer.Write(value);
            return buffer.WrittenSpan.ToArray();
        }

        var state = MemoryPackSerializerExt.WriterState;
        MemoryPackSerializerExt.WriterState = default;
        try {
            using var buffer = serializer.Write(value);
            return buffer.WrittenSpan.ToArray();
        }
        finally {
            MemoryPackSerializerExt.WriterState = state;
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
        if (serializerKind != SerializerKind.MemoryPack)
            return serializer.Read<T>(bytes);

        var state = MemoryPackSerializerExt.ReaderState;
        MemoryPackSerializerExt.ReaderState = default;
        try {
            return serializer.Read<T>(bytes);
        }
        finally {
            MemoryPackSerializerExt.ReaderState = state;
        }
    }
}
