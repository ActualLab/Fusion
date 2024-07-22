namespace ActualLab.Fusion.Internal;

public sealed record YieldFixedDelayer(RetryDelaySeq RetryDelays) : FixedDelayer(RetryDelays)
{
    public override Task Delay(int retryCount, CancellationToken cancellationToken = default)
        => RetryDelay(retryCount, cancellationToken)
           ?? TaskExt.YieldDelay();
}
