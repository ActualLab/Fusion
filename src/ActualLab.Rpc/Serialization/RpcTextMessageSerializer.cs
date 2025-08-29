namespace ActualLab.Rpc.Serialization;

public abstract class RpcTextMessageSerializer(RpcPeer peer) : RpcMessageSerializer(peer)
{
    public static class Defaults
    {
        public static int MaxArgumentDataSize { get; set; } = 130_000_000; // 130 MB;
    }
}
