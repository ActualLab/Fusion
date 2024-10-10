using System.Buffers;
using System.Text;
using ActualLab.IO;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc.Serialization;

public abstract class RpcMessageSerializer(RpcPeer peer) : IByteSerializer<RpcMessage>
{
    [ThreadStatic] private static Encoder? _utf8Encoder;
    [ThreadStatic] private static Decoder? _utf8Decoder;
    [ThreadStatic] private static ArrayPoolBuffer<byte>? _utf8EncodeBuffer;
    [ThreadStatic] private static ArrayPoolBuffer<char>? _utf8DecodeBuffer;

    public static int Utf8BufferCapacity { get; set; } = 512; // In bytes and characters, used only for header values
    public static int Utf8BufferReplaceCapacity { get; set; } = 8192; // In bytes and characters, used only for header values

    protected RpcMethodResolver ServerMethodResolver => Peer.ServerMethodResolver;

    public RpcPeer Peer { get; } = peer;

    public abstract RpcMessage Read(ReadOnlyMemory<byte> data, out int readLength);
    public abstract void Write(IBufferWriter<byte> bufferWriter, RpcMessage value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static Encoder GetUtf8Encoder()
        => _utf8Encoder ??= EncodingExt.Utf8NoBom.GetEncoder();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static Decoder GetUtf8Decoder()
        => _utf8Decoder ??= EncodingExt.Utf8NoBom.GetDecoder();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static ArrayPoolBuffer<byte> GetUtf8EncodeBuffer()
        => _utf8EncodeBuffer ??= new ArrayPoolBuffer<byte>(Utf8BufferCapacity);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static ArrayPoolBuffer<char> GetUtf8DecodeBuffer()
        => _utf8DecodeBuffer ??= new ArrayPoolBuffer<char>(Utf8BufferCapacity);
}
