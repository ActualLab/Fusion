namespace ActualLab.Fusion.Internal;

/// <summary>
/// A <see cref="FixedDelayer"/> that yields the current thread before updating.
/// </summary>
public sealed record YieldFixedDelayer(RetryDelaySeq RetryDelays) : FixedDelayer(RetryDelays)
{
    public override Task Delay(int retryCount, CancellationToken cancellationToken = default)
        => RetryDelay(retryCount, cancellationToken)
           ?? TaskExt.YieldDelay();
}
