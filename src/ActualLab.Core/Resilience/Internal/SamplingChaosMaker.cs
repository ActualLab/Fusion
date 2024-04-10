namespace ActualLab.Resilience.Internal;

public sealed record SamplingChaosMaker(Sampler Sampler, ChaosMaker Next) : ChaosMaker
{
    public override string ToString()
        => $"{Next}.Sampled({Sampler})";

    public override Task Act(object context, CancellationToken cancellationToken)
        => Sampler.Next()
            ? Next.Act(context, cancellationToken)
            : Task.CompletedTask;
}
