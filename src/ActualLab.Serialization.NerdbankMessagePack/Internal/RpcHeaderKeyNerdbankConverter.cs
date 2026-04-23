using System.Buffers;
using ActualLab.Rpc.Infrastructure;
using Nerdbank.MessagePack;

namespace ActualLab.Serialization.Internal;

/// <summary>
/// Nerdbank.MessagePack converter for <see cref="RpcHeaderKey"/>. Wire shape matches the legacy
/// MessagePack-CSharp <c>[MessagePackObject] + [Key(0)] ReadOnlyMemory&lt;byte&gt; Utf8Name</c>
/// formatter: a 1-element array containing the UTF-8 name as msgpack <c>bin</c>.
/// </summary>
public sealed class RpcHeaderKeyNerdbankConverter : MessagePackConverter<RpcHeaderKey>
{
    public override RpcHeaderKey Read(ref MessagePackReader reader, SerializationContext context)
    {
        var len = reader.ReadArrayHeader();
        if (len < 1)
            throw new MessagePackSerializationException(
                $"Expected 1+ element array for RpcHeaderKey, got {len}.");
        var seq = reader.ReadBytes();
        var utf8 = seq.HasValue ? BuffersExtensions.ToArray(seq.Value) : [];
        for (var i = 1; i < len; i++)
            reader.Skip(context);
        return new RpcHeaderKey(utf8);
    }

    public override void Write(ref MessagePackWriter writer, in RpcHeaderKey value, SerializationContext context)
    {
        writer.WriteArrayHeader(1);
        writer.Write(value.Utf8Name.Span);
    }
}
