using System.Buffers;
using ActualLab.Rpc.Caching;
using Nerdbank.MessagePack;

namespace ActualLab.Serialization.Internal;

/// <summary>
/// Nerdbank.MessagePack converter for <see cref="RpcCacheValue"/>. Wire shape matches the legacy
/// MessagePack-CSharp <c>[MessagePackObject] + [Key(0..1)]</c> formatter: a 2-element array
/// <c>[Data, Hash]</c>.
/// </summary>
public sealed class RpcCacheValueNerdbankConverter : MessagePackConverter<RpcCacheValue?>
{
    public override RpcCacheValue? Read(ref MessagePackReader reader, SerializationContext context)
    {
        if (reader.TryReadNil())
            return null;
        var len = reader.ReadArrayHeader();
        if (len < 2)
            throw new MessagePackSerializationException(
                $"Expected 2+ element array for RpcCacheValue, got {len}.");
        var seq = reader.ReadBytes();
        var data = seq.HasValue ? BuffersExtensions.ToArray(seq.Value) : [];
        var hash = reader.ReadString() ?? "";
        for (var i = 2; i < len; i++)
            reader.Skip(context);
        return new RpcCacheValue(data, hash);
    }

    public override void Write(ref MessagePackWriter writer, in RpcCacheValue? value, SerializationContext context)
    {
        if (value is null) {
            writer.WriteNil();
            return;
        }
        writer.WriteArrayHeader(2);
        writer.Write(value.Data.Span);
        writer.Write(value.Hash);
    }
}
