using System.Buffers;
using Nerdbank.MessagePack;

namespace ActualLab.Serialization.Internal;

/// <summary>
/// Nerdbank.MessagePack converter for <see cref="TypeDecoratingUniSerialized{T}"/>.
/// <para>
/// Emits the same wire format MessagePack-CSharp produced for this type via its
/// <c>[MessagePackObject] [Key(0)] MessagePackData MessagePack</c> layout: a 1-element array
/// whose single element is a <c>bin</c> carrying the type-decorated inner bytes
/// (<see cref="TypeRef"/> as a string followed by the value). Because Nerdbank and
/// MessagePack-CSharp share the msgpack wire format for primitives / strings / arrays,
/// the resulting bytes are identical across serializers for all value types the default
/// converter stacks treat the same way — which is why cross-compat is restored.
/// </para>
/// </summary>
public sealed class TypeDecoratingUniSerializedNerdbankConverter<T> : MessagePackConverter<TypeDecoratingUniSerialized<T>>
{
    public override TypeDecoratingUniSerialized<T> Read(ref MessagePackReader reader, SerializationContext context)
    {
        var len = reader.ReadArrayHeader();
        if (len != 1)
            throw new MessagePackSerializationException(
                $"Expected 1-element array for TypeDecoratingUniSerialized<{typeof(T).Name}>, got {len}.");
        var raw = reader.ReadBytes();
        if (!raw.HasValue || raw.Value.Length == 0)
            return new TypeDecoratingUniSerialized<T> { Value = default! };
        var innerReader = new MessagePackReader(raw.Value);
        var typeRefConverter = context.GetConverter<TypeRef>(context.TypeShapeProvider);
        var actualTypeRef = typeRefConverter.Read(ref innerReader, context);
        if (actualTypeRef == default)
            return new TypeDecoratingUniSerialized<T> { Value = default! };

#pragma warning disable IL2026
        var actualType = actualTypeRef.Resolve();
#pragma warning restore IL2026
        if (!typeof(T).IsAssignableFrom(actualType))
            throw Errors.UnsupportedSerializedType(actualType);
        var valueConverter = context.GetConverter(actualType, context.TypeShapeProvider);
        var value = (T?)valueConverter.ReadObject(ref innerReader, context);
        return new TypeDecoratingUniSerialized<T> { Value = value! };
    }

#pragma warning disable NBMsgPack031
    public override void Write(
        ref MessagePackWriter writer,
        in TypeDecoratingUniSerialized<T> value,
        SerializationContext context)
    {
        var buffer = new ArrayBufferWriter<byte>();
        var innerWriter = new MessagePackWriter(buffer);
        var typeRefConverter = context.GetConverter<TypeRef>(context.TypeShapeProvider);
        if (ReferenceEquals(value.Value, null))
            typeRefConverter.Write(ref innerWriter, default, context);
        else {
            var actualType = value.Value.GetType();
            var actualTypeRef = new TypeRef(actualType).WithoutAssemblyVersions();
            typeRefConverter.Write(ref innerWriter, actualTypeRef, context);
            var valueConverter = context.GetConverter(actualType, context.TypeShapeProvider);
            valueConverter.WriteObject(ref innerWriter, value.Value, context);
        }
        innerWriter.Flush();
        writer.WriteArrayHeader(1);
        writer.Write(buffer.WrittenSpan);
    }
#pragma warning restore NBMsgPack031
}
