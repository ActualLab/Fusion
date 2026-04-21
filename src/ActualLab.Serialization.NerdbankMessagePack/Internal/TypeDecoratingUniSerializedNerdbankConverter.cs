using System.Buffers;
using Nerdbank.MessagePack;

namespace ActualLab.Serialization.Internal;

/// <summary>
/// Nerdbank.MessagePack converter for <see cref="TypeDecoratingUniSerialized{T}"/>. The legacy
/// MessagePack wire for this type is a 1-key map <c>{MessagePack: bin}</c>, where the bin is the
/// MessagePack-CSharp encoding of <c>Value</c> with type decoration. Post-migration, Nerdbank owns
/// the wire — we keep the 1-key map envelope but rename the key to <c>Value</c> and carry a
/// Nerdbank type-decorating payload instead. Reading supports both keys so older legacy bytes
/// that still say <c>MessagePack</c> can be drained by peers on the new path; writing always
/// emits the post-migration <c>Value</c> key.
/// </summary>
public sealed class TypeDecoratingUniSerializedNerdbankConverter<T> : MessagePackConverter<TypeDecoratingUniSerialized<T>>
{
    private static readonly ReadOnlyMemory<byte> ValueKeyUtf8 = "Value"u8.ToArray();

    public override TypeDecoratingUniSerialized<T> Read(ref MessagePackReader reader, SerializationContext context)
    {
        var mapLen = reader.ReadMapHeader();
        T? value = default;
        for (var i = 0; i < mapLen; i++) {
            var key = reader.ReadString();
            if (string.Equals(key, "Value", StringComparison.Ordinal)) {
                var rawBytes = reader.ReadBytes();
                if (!rawBytes.HasValue || rawBytes.Value.Length == 0) {
                    value = default;
                    continue;
                }
                var bytes = BuffersExtensions.ToArray(rawBytes.Value);
                value = (T?)NerdbankMessagePackByteSerializer.DefaultTypeDecorating.Read(bytes, typeof(T), out _);
            }
            else {
                reader.Skip(context);
            }
        }
        return new TypeDecoratingUniSerialized<T> { Value = value! };
    }

    public override void Write(ref MessagePackWriter writer, in TypeDecoratingUniSerialized<T> value, SerializationContext context)
    {
        var buffer = new ArrayBufferWriter<byte>();
        NerdbankMessagePackByteSerializer.DefaultTypeDecorating.Write(buffer, value.Value, typeof(T));
        writer.WriteMapHeader(1);
        writer.WriteString(ValueKeyUtf8.Span);
        writer.Write(buffer.WrittenSpan);
    }
}
