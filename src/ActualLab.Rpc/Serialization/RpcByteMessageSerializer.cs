namespace ActualLab.Rpc.Serialization;

/// <summary>
/// Base class for binary <see cref="RpcMessageSerializer"/> implementations with shared size limits.
/// </summary>
public abstract class RpcByteMessageSerializer(RpcPeer peer) : RpcMessageSerializer(peer)
{
    /// <summary>
    /// Default configuration values for <see cref="RpcByteMessageSerializer"/>.
    /// </summary>
    public static class Defaults
    {
        // A whole message must fit RpcFrameBasedTransport.DefaultMaxFrameSize together with the
        // worst-case envelope of the most expensive registered format, so this isn't a round 16 MiB
        public static int MaxArgumentDataSize { get; set; } = 16_252_928; // 15.5 MiB
    }

    public const int MaxMethodRefSize = RpcMethodRef.MaxUtf8NameLength;
    // Fusion's own largest header value is a W3C tracestate, which the spec caps at 512 chars
    public const int MaxHeaderSize = 1024;
}
