namespace ActualLab.Rpc.WebSockets;

public static class FrameDelayerFactories
{
    public static readonly Func<FrameDelayer?>? None = null;

    public static readonly Func<FrameDelayer?>? NextTick = () => FrameDelayers.NextTick();
    public static readonly Func<FrameDelayer?>? Yield = () => FrameDelayers.Yield();
    public static readonly Func<FrameDelayer?>? DoubleYield = () => FrameDelayers.Yield(2);

    public static Func<FrameDelayer?>? Auto(RpcPeer peer, PropertyBag properties)
        => peer.Ref.IsBackend
            ? None
            : peer.Ref.IsServer
                ? NextTick // Server awaits the next tick to compose larger frames
                : Yield; // Client yields to compose larger frames
}
