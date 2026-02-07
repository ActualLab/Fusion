using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;
using Nerdbank.MessagePack;

namespace ActualLab.Serialization.Internal;

/// <summary>
/// Nerdbank.MessagePack converter for <see cref="RpcStream{T}"/>.
/// Prevents Nerdbank from treating RpcStream as IAsyncEnumerable and instead
/// serializes only the data contract members: AckPeriod, AckAdvance, SerializedId.
/// </summary>
public sealed class RpcStreamNerdbankConverter<T> : MessagePackConverter<RpcStream<T>?>
{
    public override RpcStream<T>? Read(ref MessagePackReader reader, SerializationContext context)
    {
        if (reader.TryReadNil())
            return null;

        var count = reader.ReadArrayHeader();
        var ackPeriod = count > 0 ? reader.ReadInt32() : 30;
        var ackAdvance = count > 1 ? reader.ReadInt32() : 61;
        var serializedId = count > 2
            ? new RpcObjectId(context.GetConverter<Guid>(context.TypeShapeProvider).Read(ref reader, context), reader.ReadInt64())
            : default;

        var stream = new RpcStream<T> {
            AckPeriod = ackPeriod,
            AckAdvance = ackAdvance,
            SerializedId = serializedId,
        };
        return stream;
    }

    public override void Write(ref MessagePackWriter writer, in RpcStream<T>? value, SerializationContext context)
    {
        if (value is null) {
            writer.WriteNil();
            return;
        }

        writer.WriteArrayHeader(3);
        writer.Write(value.AckPeriod);
        writer.Write(value.AckAdvance);
        var id = value.SerializedId;
        context.GetConverter<Guid>(context.TypeShapeProvider).Write(ref writer, id.HostId, context);
#pragma warning disable NBMsgPack031
        writer.Write(id.LocalId);
#pragma warning restore NBMsgPack031
    }
}
