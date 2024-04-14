using ActualLab.OS;
using ActualLab.Resilience;

namespace ActualLab.Fusion.EntityFramework.LogProcessing;

public interface IDbLogProcessor
{
    DbLogKind LogKind { get; }
}

public abstract record DbLogProcessorOptions
{
    // Gap / separate item processing settings
    public RandomTimeSpan ReprocessDelay { get; init; } = TimeSpan.FromSeconds(0.1).ToRandom(0.1);
    public IRetryPolicy ReprocessPolicy { get; init; } = null!;
    // Batch processing settings
    public int BatchSize { get; init; } = 64;
    public RandomTimeSpan CheckPeriod { get; init; } = TimeSpan.FromSeconds(5).ToRandom(0.1);
    public RetryDelaySeq RetryDelays { get; init; } = RetryDelaySeq.Exp(0.25, 5);
    public int ConcurrencyLevel { get; init; } = HardwareInfo.GetProcessorCountFactor(4);
    public LogLevel LogLevel { get; init; } = LogLevel.Information;
}

public abstract record DbOperationLogProcessorOptions : DbLogProcessorOptions
{
    public TimeSpan StartOffset { get; init; } = TimeSpan.FromSeconds(3);

    protected DbOperationLogProcessorOptions()
    {
        ReprocessPolicy = new RetryPolicy(
            10, TimeSpan.FromSeconds(30),
            RetryDelaySeq.Exp(0.25, 1, 0.1, 2)); // Up to 1 second, 2x longer on each iteration
    }
}

public abstract record DbEventLogProcessorOptions : DbLogProcessorOptions
{
    protected DbEventLogProcessorOptions()
    {
        ReprocessPolicy = new RetryPolicy(
            TimeSpan.FromMinutes(5),
            RetryDelaySeq.Exp(0.25, 1, 0.1, 2)); // Up to 1 second, 2x longer on each iteration
    }
}
