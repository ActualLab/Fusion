namespace ActualLab.Serialization.Internal;

/// <summary>
/// A base for MemoryPack formatters of <see cref="ByteSerialized{T}"/> and its descendants:
/// a version-tolerant object with a single <see cref="ByteSerialized{T}.Data"/> member.
/// </summary>
public abstract class ByteSerializedMemoryPackFormatterBase<TSerialized, T> : MemoryPackFormatter<TSerialized>
    where TSerialized : ByteSerialized<T>, new()
{
    public override void Serialize<TBufferWriter>(
        ref MemoryPackWriter<TBufferWriter> writer, scoped ref TSerialized? value)
    {
        if (value is null)
            writer.WriteNullObjectHeader();
        else
            writer.WriteBytesObject(value.Data); // Data serializes the value on every read
    }

    public override void Deserialize(ref MemoryPackReader reader, scoped ref TSerialized? value)
        => value = reader.TryReadBytesObject(out var data) ? new TSerialized() { Data = data! } : null;
}

/// <summary>
/// A base for MemoryPack formatters of <see cref="TextSerialized{T}"/> and its descendants:
/// a version-tolerant object with a single <see cref="TextSerialized{T}.Data"/> member.
/// </summary>
public abstract class TextSerializedMemoryPackFormatterBase<TSerialized, T> : MemoryPackFormatter<TSerialized>
    where TSerialized : TextSerialized<T>, new()
{
    public override void Serialize<TBufferWriter>(
        ref MemoryPackWriter<TBufferWriter> writer, scoped ref TSerialized? value)
    {
        if (value is null)
            writer.WriteNullObjectHeader();
        else
            writer.WriteStringObject(value.Data); // Data serializes the value on every read
    }

    public override void Deserialize(ref MemoryPackReader reader, scoped ref TSerialized? value)
        => value = reader.TryReadStringObject(out var data) ? new TSerialized() { Data = data! } : null;
}

public sealed class ByteSerializedMemoryPackFormatter<T>
    : ByteSerializedMemoryPackFormatterBase<ByteSerialized<T>, T>;

public sealed class MemoryPackSerializedMemoryPackFormatter<T>
    : ByteSerializedMemoryPackFormatterBase<MemoryPackSerialized<T>, T>;

public sealed class MessagePackSerializedMemoryPackFormatter<T>
    : ByteSerializedMemoryPackFormatterBase<MessagePackSerialized<T>, T>;

public sealed class TypeDecoratingMemoryPackSerializedMemoryPackFormatter<TSchema, T>
    : ByteSerializedMemoryPackFormatterBase<TypeDecoratingMemoryPackSerialized<TSchema, T>, T>
    where TSchema : TypeSchema;

public sealed class TypeDecoratingMessagePackSerializedMemoryPackFormatter<TSchema, T>
    : ByteSerializedMemoryPackFormatterBase<TypeDecoratingMessagePackSerialized<TSchema, T>, T>
    where TSchema : TypeSchema;

public sealed class TextSerializedMemoryPackFormatter<T>
    : TextSerializedMemoryPackFormatterBase<TextSerialized<T>, T>;

public sealed class SystemJsonSerializedMemoryPackFormatter<T>
    : TextSerializedMemoryPackFormatterBase<SystemJsonSerialized<T>, T>;

public sealed class TypeDecoratingSystemJsonSerializedMemoryPackFormatter<TSchema, T>
    : TextSerializedMemoryPackFormatterBase<TypeDecoratingSystemJsonSerialized<TSchema, T>, T>
    where TSchema : TypeSchema;

/// <summary>
/// MemoryPack formatter for <see cref="UniSerialized{T}"/>: a version-tolerant object
/// with a single byte array member holding the value serialized by
/// the current MemoryPack serializer (see <see cref="ByteSerializer.Current"/>).
/// </summary>
public sealed class UniSerializedMemoryPackFormatter<T> : MemoryPackFormatter<UniSerialized<T>>
{
    public override void Serialize<TBufferWriter>(
        ref MemoryPackWriter<TBufferWriter> writer, scoped ref UniSerialized<T> value)
    {
        var serializer = ByteSerializer.GetCurrent<MemoryPackByteSerializer>();
        using var buffer = NestedMemoryPackSerialization.Write(serializer, value.Value);
        writer.WriteBytesObject(buffer.WrittenSpan);
    }

    public override void Deserialize(ref MemoryPackReader reader, scoped ref UniSerialized<T> value)
    {
        if (!reader.TryReadBytesObject(out var data) || data is null) {
            value = default;
            return;
        }

        var serializer = ByteSerializer.GetCurrent<MemoryPackByteSerializer>();
        value = new UniSerialized<T>() { Value = NestedMemoryPackSerialization.Read<T>(serializer, data) };
    }
}

/// <summary>
/// MemoryPack formatter for <see cref="TypeDecoratingUniSerialized{TSchema,T}"/>: a version-tolerant
/// object with a single byte array member holding the value serialized by the
/// type-decorating counterpart of the current MemoryPack serializer, restricted by <typeparamref name="TSchema"/>.
/// </summary>
public sealed class TypeDecoratingUniSerializedMemoryPackFormatter<TSchema, T>
    : MemoryPackFormatter<TypeDecoratingUniSerialized<TSchema, T>>
    where TSchema : TypeSchema
{
    public static readonly TypeDecoratingUniSerializedMemoryPackFormatter<TSchema, T> Default = new();

    public override void Serialize<TBufferWriter>(
        ref MemoryPackWriter<TBufferWriter> writer, scoped ref TypeDecoratingUniSerialized<TSchema, T> value)
    {
        var serializer = TypeSchema<TSchema>.GetTypeDecoratingSerializer(
            ByteSerializer.GetCurrent<MemoryPackByteSerializer>(), SerializerKind.MemoryPack);
        using var buffer = NestedMemoryPackSerialization.Write(serializer, value.Value);
        writer.WriteBytesObject(buffer.WrittenSpan);
    }

    public override void Deserialize(
        ref MemoryPackReader reader, scoped ref TypeDecoratingUniSerialized<TSchema, T> value)
    {
        if (!reader.TryReadBytesObject(out var data) || data is null) {
            value = default;
            return;
        }

        var serializer = TypeSchema<TSchema>.GetTypeDecoratingSerializer(
            ByteSerializer.GetCurrent<MemoryPackByteSerializer>(), SerializerKind.MemoryPack);
        value = new TypeDecoratingUniSerialized<TSchema, T>() {
            Value = NestedMemoryPackSerialization.Read<T>(serializer, data),
        };
    }
}

/// <summary>
/// Runs a MemoryPack serialization nested into another one: MemoryPack keeps per-thread
/// reader and writer state, which the nested call must neither see nor corrupt.
/// </summary>
internal static class NestedMemoryPackSerialization
{
    public static ArrayPoolBuffer<byte> Write<T>(IByteSerializer serializer, T value)
    {
        using var stateSnapshot = MemoryPackSerializer.ResetWriterState();
        return serializer.Write(value, typeof(T));
    }

    public static T Read<T>(IByteSerializer serializer, byte[] data)
    {
        using var stateSnapshot = MemoryPackSerializer.ResetReaderState();
        return (T)serializer.Read(data, typeof(T), out _)!;
    }
}
