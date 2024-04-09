namespace ActualLab.Fusion.EntityFramework.Operations;

public class DbOperationsChaosMonkey
{
    public static DbOperationsChaosMonkey? Instance { get; set; } = null;

    public Sampler CommitDelaySampler { get; init; } = Sampler.Never;
    public Sampler CommitFailureSampler { get; init; } = Sampler.Never;
    public RandomTimeSpan CommitDelay { get; init; } = TimeSpan.FromSeconds(0.55).ToRandom(1);

    public static DbOperationsChaosMonkey New(double commitDelayOrFailureChance)
        => new() {
            CommitDelaySampler = Sampler.Random(commitDelayOrFailureChance),
            CommitFailureSampler = Sampler.Random(commitDelayOrFailureChance)
        };

    public static DbOperationsChaosMonkey New(double commitDelayChance, double commitFailureChance)
        => new() {
            CommitDelaySampler = Sampler.Random(commitDelayChance),
            CommitFailureSampler = Sampler.Random(commitFailureChance)
        };
}
