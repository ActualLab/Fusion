namespace ActualLab.Fusion.Internal;

public sealed record TaskDelayFixedDelayer(TimeSpan UpdateDelay, RetryDelaySeq RetryDelays)
    : FixedDelayer(RetryDelays)
{
    public override Task Delay(int retryCount, CancellationToken cancellationToken = default)
        => RetryDelay(retryCount, cancellationToken)
           ?? Task.Delay(UpdateDelay, cancellationToken);
}
