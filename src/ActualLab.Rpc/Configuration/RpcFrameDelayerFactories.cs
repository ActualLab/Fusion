namespace ActualLab.Rpc;

public static class RpcFrameDelayerFactories
{
    public static readonly Func<RpcFrameDelayer?>? None = null;

    public static readonly Func<RpcFrameDelayer?>? NextTick = () => RpcFrameDelayers.NextTick();
    public static readonly Func<RpcFrameDelayer?>? Yield = () => RpcFrameDelayers.Yield();
    public static readonly Func<RpcFrameDelayer?>? DoubleYield = () => RpcFrameDelayers.Yield(2);

    public static Func<RpcFrameDelayer?>? Auto(RpcPeer peer, PropertyBag properties)
        => peer.Ref.IsBackend
            ? None
            : peer.Ref.IsServer
                ? NextTick // Server awaits the next tick to compose larger frames
                : Yield; // Client yields to compose larger frames
}
