namespace ActualLab.Fusion.Internal;

public sealed record NextTickFixedDelayer(RetryDelaySeq RetryDelays)
    : FixedDelayer(RetryDelays)
{
    public override Task Delay(int retryCount, CancellationToken cancellationToken = default)
        => RetryDelay(retryCount, cancellationToken)
           ?? TickSource.Default.WhenNextTick().WaitAsync(cancellationToken);
}
