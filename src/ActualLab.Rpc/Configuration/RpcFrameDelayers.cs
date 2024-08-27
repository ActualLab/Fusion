namespace ActualLab.Rpc;

public delegate Task RpcFrameDelayer(int frameSize);
public delegate RpcFrameDelayer? RpcFrameDelayerFactory();

public static class RpcFrameDelayers
{
    public const int NoDelayFrameCount = 2; // Handshake + Reconnect frames are never delayed
    public static int DelayedFrameSize { get; set; } = 1024; // Only frames smaller than this are delayed
    public static int DefaultClientYieldCount { get; set; } = 1;

    public static Func<RpcPeer, PropertyBag, RpcFrameDelayerFactory?> DefaultFactoryProvider { get; set; }
        = static (peer, _) => {
            var peerRef = peer.Ref;
            if (peerRef.IsBackend)
                return null;

            return peerRef.IsServer
                ? static () => NextTick() // Server awaits the next tick to compose larger frames
                : static () => Yield(DefaultClientYieldCount); // Client yields to compose larger frames
        };

    public static RpcFrameDelayer? Yield(int yieldCount = 1, long handshakeFrameCount = 1)
    {
        if (yieldCount < 1)
            return null;

        long frameIndex = 0;
        return yieldCount == 1
            ? frameSize => MustDelay(frameIndex++, frameSize) ? TaskExt.YieldDelay() : Task.CompletedTask
            : frameSize => MustDelay(frameIndex++, frameSize) ? TaskExt.YieldDelay(yieldCount) : Task.CompletedTask;
    }

    public static RpcFrameDelayer NextTick(TickSource? tickSource = null)
    {
        tickSource ??= TickSource.Default;
        long frameIndex = 0;
        return frameSize => MustDelay(frameIndex++, frameSize)
            ? tickSource.WhenNextTick()
            : Task.CompletedTask;
    }

    public static RpcFrameDelayer? Delay(double delayMs)
        => Delay(TimeSpan.FromMilliseconds(delayMs));
    public static RpcFrameDelayer? Delay(TimeSpan delay)
    {
        if (delay <= TimeSpan.Zero)
            return null;

        long frameIndex = 0;
        return frameSize => MustDelay(frameIndex++, frameSize)
            ? Task.Delay(delay)
            : Task.CompletedTask;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool MustDelay(long frameIndex, int frameSize)
        => frameSize < DelayedFrameSize || frameIndex < NoDelayFrameCount;
}
