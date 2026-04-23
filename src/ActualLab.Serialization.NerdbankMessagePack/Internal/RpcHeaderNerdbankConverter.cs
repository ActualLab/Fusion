using ActualLab.Rpc.Infrastructure;
using Nerdbank.MessagePack;

namespace ActualLab.Serialization.Internal;

/// <summary>
/// Nerdbank.MessagePack converter for <see cref="RpcHeader"/>. Wire shape matches the legacy
/// MessagePack-CSharp <c>[MessagePackObject] + [Key(0..1)]</c> formatter: a 2-element array
/// <c>[Name, Value]</c>.
/// </summary>
public sealed class RpcHeaderNerdbankConverter : MessagePackConverter<RpcHeader>
{
    public override RpcHeader Read(ref MessagePackReader reader, SerializationContext context)
    {
        var len = reader.ReadArrayHeader();
        if (len < 2)
            throw new MessagePackSerializationException(
                $"Expected 2+ element array for RpcHeader, got {len}.");
        var name = reader.ReadString() ?? "";
        var value = reader.ReadString();
        for (var i = 2; i < len; i++)
            reader.Skip(context);
        return new RpcHeader(name, value);
    }

    public override void Write(ref MessagePackWriter writer, in RpcHeader value, SerializationContext context)
    {
        writer.WriteArrayHeader(2);
        writer.Write(value.Name);
        writer.Write(value.Value);
    }
}
