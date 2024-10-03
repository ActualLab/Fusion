namespace ActualLab.Rpc;

public static class RpcFrameDelayerProviders
{
    public static RpcFrameDelayerFactory? None(RpcPeer peer, PropertyBag properties)
        => null;

    public static Func<RpcPeer, PropertyBag, RpcFrameDelayerFactory?> Auto(int clientYieldCount = 1)
        => (peer, _) => {
            var peerRef = peer.Ref;
            if (peerRef.IsBackend)
                return null;

            return peerRef.IsServer
                ? () => RpcFrameDelayers.NextTick() // Server awaits the next tick to compose larger frames
                : () => RpcFrameDelayers.Yield(clientYieldCount); // Client yields to compose larger frames
        };
}
