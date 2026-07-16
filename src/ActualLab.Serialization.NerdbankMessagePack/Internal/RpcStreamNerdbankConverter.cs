using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;
using Nerdbank.MessagePack;

namespace ActualLab.Serialization.Internal;

/// <summary>
/// Nerdbank.MessagePack converter for <see cref="RpcStream{T}"/>.
/// Prevents Nerdbank from treating RpcStream as IAsyncEnumerable and instead
/// serializes only the data contract members matching the MessagePack name-based format:
/// { "AckPeriod": int, "AckAdvance": int, "SerializedId": [Guid, long] }
/// </summary>
public sealed class RpcStreamNerdbankConverter<T> : MessagePackConverter<RpcStream<T>?>
{
    private static ReadOnlySpan<byte> AckPeriodKey => "AckPeriod"u8;
    private static ReadOnlySpan<byte> AckAdvanceKey => "AckAdvance"u8;
    private static ReadOnlySpan<byte> SerializedIdKey => "SerializedId"u8;

    public override RpcStream<T>? Read(ref MessagePackReader reader, SerializationContext context)
    {
        if (reader.TryReadNil())
            return null;

        var count = reader.ReadMapHeader();
        var ackPeriod = 30;
        var ackAdvance = 61;
        var serializedId = default(RpcObjectId);

        for (var i = 0; i < count; i++) {
            var key = reader.ReadStringSpan();
            if (key.SequenceEqual(AckPeriodKey))
                ackPeriod = reader.ReadInt32();
            else if (key.SequenceEqual(AckAdvanceKey))
                ackAdvance = reader.ReadInt32();
            else if (key.SequenceEqual(SerializedIdKey))
                serializedId = ReadRpcObjectId(ref reader, context);
            else
                reader.Skip(context);
        }

        return new RpcStream<T> {
            AckPeriod = ackPeriod,
            AckAdvance = ackAdvance,
            SerializedId = serializedId,
        };
    }

#pragma warning disable NBMsgPack031
    public override void Write(ref MessagePackWriter writer, in RpcStream<T>? value, SerializationContext context)
#pragma warning restore NBMsgPack031
    {
        if (value is null) {
            writer.WriteNil();
            return;
        }

        writer.WriteMapHeader(3);
        writer.WriteString(AckPeriodKey);
        writer.Write(value.AckPeriod);
        writer.WriteString(AckAdvanceKey);
        writer.Write(value.AckAdvance);
        writer.WriteString(SerializedIdKey);
        WriteRpcObjectId(ref writer, value.SerializedId, context);
    }

    private static RpcObjectId ReadRpcObjectId(ref MessagePackReader reader, SerializationContext context)
        => context.GetConverter<RpcObjectId>(context.TypeShapeProvider).Read(ref reader, context);

    private static void WriteRpcObjectId(ref MessagePackWriter writer, RpcObjectId id, SerializationContext context)
        => context.GetConverter<RpcObjectId>(context.TypeShapeProvider).Write(ref writer, id, context);
}
