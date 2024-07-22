namespace ActualLab.Resilience.Internal;

public sealed record JoinChaosMaker(ChaosMaker First, ChaosMaker Second) : ChaosMaker
{
    public override string ToString()
        => $"({First} | {Second})";

    public override async Task Act(object context, CancellationToken cancellationToken)
    {
        await First.Act(context, cancellationToken).ConfigureAwait(false);
        await Second.Act(context, cancellationToken).ConfigureAwait(false);
    }
}
