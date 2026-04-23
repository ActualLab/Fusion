using ActualLab.Rpc.Infrastructure;
using Nerdbank.MessagePack;

namespace ActualLab.Serialization.Internal;

/// <summary>
/// Nerdbank.MessagePack converter for <see cref="RpcObjectId"/>. Wire shape matches the legacy
/// MessagePack-CSharp <c>[MessagePackObject] + [Key(0..1)]</c> formatter: a 2-element array
/// <c>[HostId, LocalId]</c>.
/// </summary>
public sealed class RpcObjectIdNerdbankConverter : MessagePackConverter<RpcObjectId>
{
    public override RpcObjectId Read(ref MessagePackReader reader, SerializationContext context)
    {
        var len = reader.ReadArrayHeader();
        if (len < 2)
            throw new MessagePackSerializationException(
                $"Expected 2+ element array for RpcObjectId, got {len}.");
        var guidConverter = context.GetConverter<Guid>(context.TypeShapeProvider);
        var hostId = guidConverter.Read(ref reader, context);
        var localId = reader.ReadInt64();
        for (var i = 2; i < len; i++)
            reader.Skip(context);
        return new RpcObjectId(hostId, localId);
    }

    public override void Write(ref MessagePackWriter writer, in RpcObjectId value, SerializationContext context)
    {
        writer.WriteArrayHeader(2);
        var guidConverter = context.GetConverter<Guid>(context.TypeShapeProvider);
        guidConverter.Write(ref writer, value.HostId, context);
        writer.Write(value.LocalId);
    }
}
