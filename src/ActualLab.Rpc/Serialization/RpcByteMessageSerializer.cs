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
        public static int MaxArgumentDataSize { get; set; } = 130_000_000; // 130 MB;
    }

    public const int MaxMethodRefSize = 65536;
    public const int MaxHeaderSize = 65536;
}
