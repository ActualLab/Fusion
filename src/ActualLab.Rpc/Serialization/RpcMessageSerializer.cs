using System.Text;
using ActualLab.IO;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc.Serialization;

/// <summary>
/// Delegate that reads an <see cref="RpcInboundMessage"/> from serialized byte data.
/// </summary>
public delegate RpcInboundMessage RpcMessageSerializerReadFunc(ReadOnlyMemory<byte> data, out int readLength);

/// <summary>
/// Delegate that writes an <see cref="RpcOutboundMessage"/> into a byte buffer.
/// </summary>
public delegate void RpcMessageSerializerWriteFunc(ArrayPoolBuffer<byte> buffer, RpcOutboundMessage message);

/// <summary>
/// Base class for serializers that read and write complete RPC messages including headers and arguments.
/// </summary>
public abstract class RpcMessageSerializer(RpcPeer peer)
{
    // Delegates for ReadXxx and WriteXxx

    [ThreadStatic] private static Encoder? _utf8Encoder;
    [ThreadStatic] private static Decoder? _utf8Decoder;
    [ThreadStatic] private static ArrayPoolBuffer<byte>? _utf8EncodeBuffer;
    [ThreadStatic] private static ArrayPoolBuffer<char>? _utf8DecodeBuffer;

    public static int Utf8BufferCapacity { get; set; } = 512; // In bytes and characters, used only for header values
    public static int Utf8BufferReplaceCapacity { get; set; } = 8192; // In bytes and characters, used only for header values

    protected RpcMethodResolver ServerMethodResolver => Peer.ServerMethodResolver;

    public RpcPeer Peer { get; } = peer;
    public virtual bool PersistsMessageSize => false;
    public virtual bool SupportsNativeLittleEndian => false;
    public RpcMessageSerializerReadFunc ReadFunc
        => field ??= SupportsNativeLittleEndian ? ReadNativeLittleEndian : Read;
    public RpcMessageSerializerWriteFunc WriteFunc
        => field ??= SupportsNativeLittleEndian ? WriteNativeLittleEndian : Write;

    public abstract RpcInboundMessage Read(ReadOnlyMemory<byte> data, out int readLength);
    public abstract void Write(ArrayPoolBuffer<byte> buffer, RpcOutboundMessage message);

    public virtual RpcInboundMessage ReadNativeLittleEndian(ReadOnlyMemory<byte> data, out int readLength)
        => throw new NotSupportedException();
    public virtual void WriteNativeLittleEndian(ArrayPoolBuffer<byte> buffer, RpcOutboundMessage message)
        => throw new NotSupportedException();

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
