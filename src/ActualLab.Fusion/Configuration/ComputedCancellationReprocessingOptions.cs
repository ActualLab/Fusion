namespace ActualLab.Fusion;

public sealed record ComputedCancellationReprocessingOptions
{
    public static ComputedCancellationReprocessingOptions Default { get; set; } = new();
    public static ComputedCancellationReprocessingOptions ClientDefault { get; set; } = new() {
        MaxTryCount = 3,
        MaxDuration = TimeSpan.FromSeconds(5),
    };
    public static ComputedCancellationReprocessingOptions None { get; } = new() {
        MaxTryCount = 1,
    };

    public int MaxTryCount { get; init; } = 10;
    public TimeSpan MaxDuration { get; init; } = TimeSpan.FromSeconds(2);
    public RetryDelaySeq RetryDelays { get; init; } = RetryDelaySeq.Exp(0.05, 1);
}
