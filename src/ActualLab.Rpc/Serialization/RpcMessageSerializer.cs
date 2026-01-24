using System.Text;
using ActualLab.IO;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc.Serialization;

public abstract class RpcMessageSerializer(RpcPeer peer)
{
    [ThreadStatic] private static Encoder? _utf8Encoder;
    [ThreadStatic] private static Decoder? _utf8Decoder;
    [ThreadStatic] private static ArrayPoolBuffer<byte>? _utf8EncodeBuffer;
    [ThreadStatic] private static ArrayPoolBuffer<char>? _utf8DecodeBuffer;

    public static int Utf8BufferCapacity { get; set; } = 512; // In bytes and characters, used only for header values
    public static int Utf8BufferReplaceCapacity { get; set; } = 8192; // In bytes and characters, used only for header values

    protected RpcMethodResolver ServerMethodResolver => Peer.ServerMethodResolver;

    public RpcPeer Peer { get; } = peer;

    public abstract RpcInboundMessage Read(ArrayPoolArrayHandle<byte> buffer, int offset, out int readLength);
    public abstract void Write(ArrayPoolBuffer<byte> buffer, RpcOutboundMessage message);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static Encoder GetUtf8Encoder()
        => _utf8Encoder ??= EncodingExt.Utf8NoBom.GetEncoder();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static Decoder GetUtf8Decoder()
        => _utf8Decoder ??= EncodingExt.Utf8NoBom.GetDecoder();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static ArrayPoolBuffer<byte> GetUtf8EncodeBuffer()
        => ArrayPoolBuffer<byte>.NewOrRenew(ref _utf8EncodeBuffer, Utf8BufferCapacity, Utf8BufferReplaceCapacity, false);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static ArrayPoolBuffer<char> GetUtf8DecodeBuffer()
        => ArrayPoolBuffer<char>.NewOrRenew(ref _utf8DecodeBuffer, Utf8BufferCapacity, Utf8BufferReplaceCapacity, false);
}
