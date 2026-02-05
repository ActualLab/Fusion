namespace ActualLab.Fusion.Internal;

/// <summary>
/// A <see cref="FixedDelayer"/> that uses <see cref="Task.Delay(TimeSpan, CancellationToken)"/>
/// with a fixed update delay.
/// </summary>
public sealed record TaskDelayFixedDelayer(TimeSpan UpdateDelay, RetryDelaySeq RetryDelays)
    : FixedDelayer(RetryDelays)
{
    public override Task Delay(int retryCount, CancellationToken cancellationToken = default)
        => RetryDelay(retryCount, cancellationToken)
           ?? Task.Delay(UpdateDelay, cancellationToken);
}
