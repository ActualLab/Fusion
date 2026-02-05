namespace ActualLab.Resilience.Internal;

/// <summary>
/// A <see cref="ChaosMaker"/> that probabilistically applies its target based on
/// a <see cref="Sampler"/>.
/// </summary>
public sealed record SamplingChaosMaker(Sampler Sampler, ChaosMaker Next) : ChaosMaker
{
    public override string ToString()
        => $"{Next}.Sampled({Sampler})";

    public override Task Act(object context, CancellationToken cancellationToken)
        => Sampler.Next()
            ? Next.Act(context, cancellationToken)
            : Task.CompletedTask;
}
