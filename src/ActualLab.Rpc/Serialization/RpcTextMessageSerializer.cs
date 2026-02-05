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
        public static int MaxArgumentDataSize { get; set; } = 130_000_000; // 130 MB;
    }
}
