namespace ActualLab.Resilience.Internal;

/// <summary>
/// A no-op <see cref="ChaosMaker"/> that introduces no faults.
/// </summary>
public sealed record NoneChaosMaker : ChaosMaker
{
    internal NoneChaosMaker() { }

    public override string ToString()
        => $"{nameof(ChaosMaker)}.{nameof(None)}";

    public override Task Act(object context, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
