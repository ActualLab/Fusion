using ActualLab.Interception;
using ActualLab.IO.Internal;
using ActualLab.Rpc.Serialization.Internal;
using Cysharp.Text;

namespace ActualLab.Rpc.Serialization;

public sealed class RpcTextArgumentSerializerV3(ITextSerializer baseSerializer)
    : RpcArgumentSerializer(false)
{
    private static readonly byte Delimiter = 0x1e; // Record separator in ASCII / UTF8

    [ThreadStatic] private static Utf8TextWriter? _utf8Buffer;
    public static int Utf8BufferReplaceCapacity { get; set; } = 65536;

    public override ReadOnlyMemory<byte> Serialize(ArgumentList arguments, bool needsPolymorphism, int sizeHint)
    {
        var writer = _utf8Buffer ??= new Utf8TextWriter();
        try {
            var itemTypes = arguments.Type.ItemTypes;
            for (var i = 0; i < itemTypes.Length; i++) {
                var type = itemTypes[i];
                if (type == typeof(CancellationToken))
                    continue;

                var item = arguments.GetUntyped(i);
                if (needsPolymorphism && IsPolymorphic(type)) {
                    var itemType = item?.GetType() ?? type;
                    TextTypeSerializer.WriteDerivedItemType(writer, type, itemType);
                    baseSerializer.Write(writer, item, itemType);
                }
                else
                    baseSerializer.Write(writer, item, type);
                writer.WriteLiteral(Delimiter);
            }

            var span = writer.Buffer.AsSpan();
            if (span.Length >= 1 && span[^1] == Delimiter)
                span = span[..^1];
            return span.ToArray();
        }
        finally {
            ref var buffer = ref writer.Buffer;
            if (buffer.Length <= Utf8BufferReplaceCapacity)
                buffer.Clear();
            else {
                buffer.Dispose();
                buffer = new Utf8ValueStringBuilder();
            }
        }
    }

    public override void Deserialize(ref ArgumentList arguments, bool needsPolymorphism, ReadOnlyMemory<byte> data)
    {
        var itemTypes = arguments.Type.ItemTypes;
        for (var i = 0; i < itemTypes.Length; i++) {
            var type = itemTypes[i];
            if (type == typeof(CancellationToken)) {
                arguments.SetCancellationToken(i, default);
                continue;
            }

            object? item;
            if (needsPolymorphism && IsPolymorphic(type)) {
                var itemType = TextTypeSerializer.ReadDerivedItemType(ref data, type);
                item = baseSerializer.ReadDelimited(ref data, itemType, Delimiter);
            }
            else
                item = baseSerializer.ReadDelimited(ref data, type, Delimiter);
            arguments.SetUntyped(i, item);
        }
    }
}
