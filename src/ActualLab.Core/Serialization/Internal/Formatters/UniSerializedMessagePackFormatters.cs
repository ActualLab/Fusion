using System.Buffers;
using MessagePack;
using MessagePack.Formatters;

namespace ActualLab.Serialization.Internal;

/// <summary>
/// A MessagePack formatter for <see cref="UniSerialized{T}"/>: a single-item array holding
/// the value serialized by the current MessagePack serializer.
/// </summary>
public sealed class UniSerializedMessagePackFormatter<T> : IMessagePackFormatter<UniSerialized<T>>
{
    public void Serialize(ref MessagePackWriter writer, UniSerialized<T> value, MessagePackSerializerOptions options)
        => UniSerializedMessagePackFormat.Write(ref writer, GetSerializer(), value.Value);

    public UniSerialized<T> Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        => UniSerializedMessagePackFormat.TryRead<T>(ref reader, options, GetSerializer(), out var value)
            ? new UniSerialized<T> { Value = value }
            : default;

    private static IByteSerializer GetSerializer()
        => ByteSerializer.GetCurrent<MessagePackByteSerializer>();
}

/// <summary>
/// A MessagePack formatter for <see cref="TypeDecoratingUniSerialized{TSchema,T}"/>: a single-item array
/// holding the value serialized by the type-decorating counterpart of the current MessagePack serializer.
/// </summary>
public sealed class TypeDecoratingUniSerializedMessagePackFormatter<TSchema, T>
    : IMessagePackFormatter<TypeDecoratingUniSerialized<TSchema, T>>
    where TSchema : TypeSchema, new()
{
    public void Serialize(
        ref MessagePackWriter writer,
        TypeDecoratingUniSerialized<TSchema, T> value,
        MessagePackSerializerOptions options)
        => UniSerializedMessagePackFormat.Write(ref writer, GetSerializer(), value.Value);

    public TypeDecoratingUniSerialized<TSchema, T> Deserialize(
        ref MessagePackReader reader,
        MessagePackSerializerOptions options)
        => UniSerializedMessagePackFormat.TryRead<T>(ref reader, options, GetSerializer(), out var value)
            ? new TypeDecoratingUniSerialized<TSchema, T> { Value = value }
            : default;

    private static IByteSerializer GetSerializer()
    {
        var serializer = ByteSerializer.GetCurrent<MessagePackByteSerializer>();
        return TypeSchema<TSchema>.GetTypeDecoratingSerializer(serializer, SerializerKind.MessagePack);
    }
}

/// <summary>
/// The envelope <see cref="UniSerializedMessagePackFormatter{T}"/> and its type-decorating variant share.
/// </summary>
public static class UniSerializedMessagePackFormat
{
    public static void Write<T>(ref MessagePackWriter writer, IByteSerializer serializer, T value)
    {
        using var buffer = serializer.Write(value, typeof(T));
        writer.WriteArrayHeader(1);
        writer.Write(buffer.WrittenSpan);
    }

    public static bool TryRead<T>(
        ref MessagePackReader reader,
        MessagePackSerializerOptions options,
        IByteSerializer serializer,
        out T value)
    {
        value = default!;
        if (reader.TryReadNil())
            return false;

        options.Security.DepthStep(ref reader);
        try {
            var count = reader.ReadArrayHeader();
            for (var i = 0; i < count; i++) {
                if (i != 0) {
                    reader.Skip();
                    continue;
                }

                var bytes = reader.ReadBytes()?.ToArray();
                if (bytes is not null)
                    value = (T)serializer.Read(bytes, typeof(T), out _)!;
            }
            return true;
        }
        finally {
            reader.Depth--;
        }
    }
}
