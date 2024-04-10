using ActualLab.OS;
using ActualLab.Resilience;

namespace ActualLab.Fusion.EntityFramework.LogProcessing;

public abstract record DbLogProcessorOptions
{
    // Gap processing settings
    public IRetryPolicy GapRetryPolicy { get; init; } = null!;
    public RandomTimeSpan GapRetryDelay { get; init; } = TimeSpan.FromSeconds(0.1).ToRandom(0.1);
    // Batch processing settings
    public int BatchSize { get; init; } = 64;
    public RetryDelaySeq RetryDelays { get; init; } = RetryDelaySeq.Exp(1, 5);
    public RandomTimeSpan ForcedCheckPeriod { get; init; } = TimeSpan.FromSeconds(5).ToRandom(0.1);
    public int ConcurrencyLevel { get; init; } = HardwareInfo.GetProcessorCountFactor(4);
    public LogLevel LogLevel { get; init; } = LogLevel.Information;
}

public abstract record DbOperationLogProcessorOptions : DbLogProcessorOptions
{
    public TimeSpan StartOffset { get; init; } = TimeSpan.FromSeconds(3);

    protected DbOperationLogProcessorOptions()
    {
        GapRetryPolicy = new RetryPolicy(
            10, TimeSpan.FromSeconds(30),
            RetryDelaySeq.Exp(0.25, 1, 0.1, 2)); // Up to 1 second, 2x longer on each iteration
    }
}

public abstract record DbEventLogProcessorOptions : DbLogProcessorOptions
{
    protected DbEventLogProcessorOptions()
    {
        GapRetryPolicy = new RetryPolicy(
            TimeSpan.FromMinutes(5),
            RetryDelaySeq.Exp(0.25, 1, 0.1, 2)); // Up to 1 second, 2x longer on each iteration
    }
}

public abstract record DbTimerLogProcessorOptions : DbLogProcessorOptions
{
    protected DbTimerLogProcessorOptions()
    {
        GapRetryPolicy = new RetryPolicy(
            TimeSpan.FromMinutes(5),
            RetryDelaySeq.Exp(0.25, 1, 0.1, 2)); // Up to 1 second, 2x longer on each iteration
    }
}
