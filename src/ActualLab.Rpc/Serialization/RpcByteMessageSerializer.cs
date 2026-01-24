namespace ActualLab.Rpc.Serialization;

public abstract class RpcByteMessageSerializer(RpcPeer peer) : RpcMessageSerializer(peer)
{
    public static class Defaults
    {
        public static int MaxArgumentDataSize { get; set; } = 130_000_000; // 130 MB;
    }

    public const int MaxMethodRefSize = 65536;
    public const int MaxHeaderSize = 65536;
}
