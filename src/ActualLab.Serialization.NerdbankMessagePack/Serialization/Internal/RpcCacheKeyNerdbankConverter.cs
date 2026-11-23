using System.Buffers;
using ActualLab.Rpc.Caching;
using Nerdbank.MessagePack;

namespace ActualLab.Serialization.Internal;

/// <summary>
/// Nerdbank.MessagePack converter for <see cref="RpcCacheKey"/>. Wire shape matches the legacy
/// MessagePack-CSharp <c>[MessagePackObject] + [Key(0..1)]</c> formatter: a 2-element array
/// <c>[Name, ArgumentData]</c>.
/// </summary>
public sealed class RpcCacheKeyNerdbankConverter : MessagePackConverter<RpcCacheKey?>
{
    public override RpcCacheKey? Read(ref MessagePackReader reader, SerializationContext context)
    {
        if (reader.TryReadNil())
            return null;
        var len = reader.ReadArrayHeader();
        if (len < 2)
            throw new MessagePackSerializationException(
                $"Expected 2+ element array for RpcCacheKey, got {len}.");
        var name = reader.ReadString() ?? "";
        var seq = reader.ReadBytes();
        var argumentData = seq.HasValue ? BuffersExtensions.ToArray(seq.Value) : [];
        for (var i = 2; i < len; i++)
            reader.Skip(context);
        return new RpcCacheKey(name, argumentData);
    }

    public override void Write(ref MessagePackWriter writer, in RpcCacheKey? value, SerializationContext context)
    {
        if (value is null) {
            writer.WriteNil();
            return;
        }
        writer.WriteArrayHeader(2);
        writer.Write(value.Name);
        writer.Write(value.ArgumentData.Span);
    }
}
