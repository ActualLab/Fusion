namespace ActualLab.Rpc.WebSockets;

public delegate Task FrameDelayer(int frameSize);

public static class FrameDelayers
{
    public const int NoDelayFrameCount = 2; // Handshake + Reconnect frames are never delayed
    public static int DelayedFrameSize { get; set; } = 1024; // Only frames smaller than this are delayed

    // Frame delayers (can used by providers)

    public static FrameDelayer? Yield(int yieldCount = 1, long handshakeFrameCount = 1)
    {
        if (yieldCount < 1)
            return null;

        long frameIndex = 0;
        return yieldCount == 1
            ? frameSize => MustDelay(frameIndex++, frameSize) ? TaskExt.YieldDelay() : Task.CompletedTask
            : frameSize => MustDelay(frameIndex++, frameSize) ? TaskExt.YieldDelay(yieldCount) : Task.CompletedTask;
    }

    public static FrameDelayer NextTick(TickSource? tickSource = null)
    {
        tickSource ??= TickSource.Default;
        long frameIndex = 0;
        return frameSize => MustDelay(frameIndex++, frameSize)
            ? tickSource.WhenNextTick()
            : Task.CompletedTask;
    }

    public static FrameDelayer? NoDelay()
        => null;

    public static FrameDelayer? Delay(double delayMs)
        => Delay(TimeSpan.FromMilliseconds(delayMs));
    public static FrameDelayer? Delay(TimeSpan delay)
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
