using ActualLab.Interception;
using ActualLab.IO.Internal;
using ActualLab.Rpc.Internal;

namespace ActualLab.Rpc.Serialization;

/// <summary>
/// A "no polymorphism" variant of <see cref="RpcTextArgumentSerializerV4"/>
/// that throws when polymorphic serialization is requested.
/// </summary>
// ReSharper disable once InconsistentNaming
public sealed class RpcTextArgumentSerializerV4NP(ITextSerializer baseSerializer) : RpcArgumentSerializer
{
    private static readonly byte Delimiter = 0x1F;

    [ThreadStatic] private static Utf8TextWriter? _utf8Buffer;
    public static int Utf8BufferReplaceCapacity { get; set; } = 65536 * 2;

    public override void Serialize(ArgumentList arguments, bool needsPolymorphism, ArrayPoolBuffer<byte> buffer)
    {
        var writer = _utf8Buffer ??= new Utf8TextWriter();
        try {
            var itemTypes = arguments.Type.ItemTypes;
            for (var i = 0; i < itemTypes.Length; i++) {
                var type = itemTypes[i];
                if (type == typeof(CancellationToken))
                    continue;

                var item = arguments.GetUntyped(i);
                if (needsPolymorphism && IsPolymorphic(type) && item is not null) {
                    var itemType = item.GetType();
                    if (itemType != type)
                        throw Errors.PolymorphicObjectButNonPolymorphicSerializer(type, itemType);
                }
                baseSerializer.Write(writer, item, type);
                writer.WriteLiteral(Delimiter);
            }

            var span = writer.Buffer.AsSpan();
            if (span.Length >= 1 && span[^1] == Delimiter)
                span = span[..^1];

            var dest = buffer.GetSpan(span.Length);
            span.CopyTo(dest);
            buffer.Advance(span.Length);
        }
        finally {
            writer.Renew(Utf8BufferReplaceCapacity);
        }
    }

    public override void Deserialize(ref ArgumentList arguments, bool needsPolymorphism, ReadOnlyMemory<byte> data)
    {
        // Deserialization never encounters polymorphic data because Serialize already rejects it.
        // We just deserialize as the declared type.
        var itemTypes = arguments.Type.ItemTypes;
        for (var i = 0; i < itemTypes.Length; i++) {
            var type = itemTypes[i];
            if (type == typeof(CancellationToken)) {
                arguments.SetCancellationToken(i, default);
                continue;
            }

            arguments.SetUntyped(i, baseSerializer.ReadDelimited(ref data, type, Delimiter));
        }
    }
}
