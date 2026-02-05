namespace ActualLab.Fusion.Internal;

/// <summary>
/// A <see cref="FixedDelayer"/> with zero update delay that completes immediately.
/// </summary>
public sealed record ZeroFixedDelayer(RetryDelaySeq RetryDelays) : FixedDelayer(RetryDelays)
{
    public override Task Delay(int retryCount, CancellationToken cancellationToken = default)
        => RetryDelay(retryCount, cancellationToken)
           ?? Task.CompletedTask;
}
