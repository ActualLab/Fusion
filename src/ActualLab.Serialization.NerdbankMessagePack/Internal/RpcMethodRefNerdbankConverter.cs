using System.Buffers;
using ActualLab.Rpc;
using Nerdbank.MessagePack;

namespace ActualLab.Serialization.Internal;

/// <summary>
/// Nerdbank.MessagePack converter for <see cref="RpcMethodRef"/>. Wire shape matches the legacy
/// MessagePack-CSharp <c>[MessagePackObject] + [Key(0)] ReadOnlyMemory&lt;byte&gt; Utf8Name</c>
/// formatter: a 1-element array containing the UTF-8 name as msgpack <c>bin</c>.
/// </summary>
public sealed class RpcMethodRefNerdbankConverter : MessagePackConverter<RpcMethodRef>
{
    public override RpcMethodRef Read(ref MessagePackReader reader, SerializationContext context)
    {
        var len = reader.ReadArrayHeader();
        if (len < 1)
            throw new MessagePackSerializationException(
                $"Expected 1+ element array for RpcMethodRef, got {len}.");
        var seq = reader.ReadBytes();
        var utf8 = seq.HasValue ? BuffersExtensions.ToArray(seq.Value) : [];
        for (var i = 1; i < len; i++)
            reader.Skip(context);
        return new RpcMethodRef(utf8);
    }

    public override void Write(ref MessagePackWriter writer, in RpcMethodRef value, SerializationContext context)
    {
        writer.WriteArrayHeader(1);
        writer.Write(value.Utf8Name.Span);
    }
}
