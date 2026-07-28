namespace ActualLab.Rpc.Serialization;

/// <summary>
/// Base class for text-based <see cref="RpcMessageSerializer"/> implementations with shared size limits.
/// </summary>
public abstract class RpcTextMessageSerializer(RpcPeer peer) : RpcMessageSerializer(peer)
{
    /// <summary>
    /// Default configuration values for <see cref="RpcTextMessageSerializer"/>.
    /// </summary>
    public static class Defaults
    {
        // A whole message must fit RpcFrameBasedTransport.DefaultMaxFrameSize together with the
        // worst-case envelope of the most expensive registered format, so this isn't a round 16 MiB
        public static int MaxArgumentDataSize { get; set; } = 16_252_928; // 15.5 MiB
    }
}
