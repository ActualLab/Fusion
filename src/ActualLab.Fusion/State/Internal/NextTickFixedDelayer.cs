namespace ActualLab.Fusion.Internal;

/// <summary>
/// A <see cref="FixedDelayer"/> that waits until the next system tick before updating.
/// </summary>
public sealed record NextTickFixedDelayer(RetryDelaySeq RetryDelays)
    : FixedDelayer(RetryDelays)
{
    public override Task Delay(int retryCount, CancellationToken cancellationToken = default)
        => RetryDelay(retryCount, cancellationToken)
           ?? TickSource.Default.WhenNextTick().WaitAsync(cancellationToken);
}
