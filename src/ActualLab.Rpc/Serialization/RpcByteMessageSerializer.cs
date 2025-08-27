namespace ActualLab.Rpc.Serialization;

public abstract class RpcByteMessageSerializer(RpcPeer peer) : RpcMessageSerializer(peer)
{
    public static class Defaults
    {
        public static bool AllowProjection { get; set; } = false;
        public static int MinProjectionSize { get; set; } = 8192;
        public static int MaxInefficiencyFactor { get; set; } = 4;
    }
}
