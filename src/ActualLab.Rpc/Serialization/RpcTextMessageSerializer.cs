using System.Buffers;
using ActualLab.Internal;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Serialization.Internal;

namespace ActualLab.Rpc.Serialization;

#pragma warning disable MA0069

public sealed class RpcTextMessageSerializer(RpcPeer peer)
    : RpcMessageSerializer(peer), ITextSerializer<RpcMessage>
{
    private static readonly byte Delimiter = (byte)'\n';
    private static readonly byte[] DelimiterBytes = [Delimiter];

    public bool PreferStringApi => false;

    public RpcMessage Read(string data)
        => throw Errors.NotSupported("This method shouldn't be used.");

    public RpcMessage Read(ReadOnlyMemory<char> data)
        => throw Errors.NotSupported("This method shouldn't be used.");

    public override RpcMessage Read(ReadOnlyMemory<byte> data, out int readLength)
    {
        var reader = new Utf8JsonReader(data.Span);
        var m = (JsonRpcMessage)JsonSerializer.Deserialize(ref reader, typeof(JsonRpcMessage), JsonRpcMessageContext.Default)!;
        var methodRef = ServerMethodResolver[m.Method ?? ""]?.Ref ?? new RpcMethodRef(m.Method ?? "");

        var tail = data.Slice((int)reader.BytesConsumed).Span;
        if (tail[0] == Delimiter)
            tail = tail[1..];

        var argumentData = (ReadOnlyMemory<byte>)tail.ToArray();
        var result = new RpcMessage(m.CallType, m.RelatedId, methodRef, argumentData, m.ParseHeaders());
        readLength = data.Length;
        return result;
    }

    public string Write(RpcMessage value)
        => throw Errors.NotSupported("This method shouldn't be used.");

    public void Write(TextWriter textWriter, RpcMessage value)
        => throw Errors.NotSupported("This method shouldn't be used.");

    public override void Write(IBufferWriter<byte> bufferWriter, RpcMessage value)
    {
        var writer = new Utf8JsonWriter(bufferWriter);
        JsonSerializer.Serialize(writer, new JsonRpcMessage(value), typeof(JsonRpcMessage), JsonRpcMessageContext.Default);
        writer.Flush();
        bufferWriter.Write(DelimiterBytes);
        bufferWriter.Write(value.ArgumentData.Span);
    }
}
