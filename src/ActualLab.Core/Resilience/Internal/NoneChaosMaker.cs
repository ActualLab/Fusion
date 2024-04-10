namespace ActualLab.Resilience.Internal;

public sealed record NoneChaosMaker : ChaosMaker
{
    internal NoneChaosMaker() { }

    public override string ToString()
        => $"{nameof(ChaosMaker)}.{nameof(None)}";

    public override Task Act(object context, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
