namespace ActualLab.Resilience.Internal;

public sealed record DelayChaosMaker(RandomTimeSpan Duration, ChaosMaker? Next = null) : ChaosMaker
{
    public override string ToString()
        => Next == null
            ? $"Delay({Duration})"
            : $"{Next}.Delayed({Duration})";

    public override Task Act(object context, CancellationToken cancellationToken)
        => Task.Delay(Duration.Next(), cancellationToken);

    public static implicit operator DelayChaosMaker(TimeSpan duration) => new(duration);
    public static implicit operator DelayChaosMaker(RandomTimeSpan duration) => new(duration);
}
