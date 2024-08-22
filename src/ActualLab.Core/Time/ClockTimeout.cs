namespace ActualLab.Time;

[StructLayout(LayoutKind.Auto)]
public readonly record struct ClockTimeout(
    MomentClock Clock,
    TimeSpan Duration)
{
    public override string ToString()
        => $"{GetType().Name}({Duration.ToShortString()})";

    public Task Wait(CancellationToken cancellationToken = default)
        => Clock.Delay(Duration, cancellationToken);

    public async Task WaitAndThrow(CancellationToken cancellationToken = default)
    {
        await Wait(cancellationToken).ConfigureAwait(false);
        throw new TimeoutException();
    }
}
