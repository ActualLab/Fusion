using ActualLab.OS;
using ActualLab.Resilience;

namespace ActualLab.Fusion.EntityFramework.LogProcessing;

/// <summary>
/// Defines the contract for a service that reads and processes database log entries.
/// </summary>
public interface IDbLogReader
{
    public DbLogKind LogKind { get; }
}

/// <summary>
/// Base configuration options for database log readers, including batch processing,
/// retry, and concurrency settings.
/// </summary>
public abstract record DbLogReaderOptions
{
    // Item retention settings, typically you need both of KeepXxx options to be true:
    // - For Operations, they have to be replayed on all hosts, so removing them instantly
    //   will prevent other hosts from processing them.
    // - For Events, even though they are only processed once, there are options like
    //   KeyConflictStrategy.Skip, which typically require processed events to be stored
    //   for a while.
    public bool KeepProcessedItems { get; init; } = true;
    public bool KeepDiscardedItems { get; init; } = true;
    // Gap / separate item processing settings
    public RandomTimeSpan ReprocessDelay { get; init; } = TimeSpan.FromSeconds(0.1).ToRandom(0.1);
    public IRetryPolicy ReprocessPolicy { get; init; } = null!;
    // Batch processing settings
    public int BatchSize { get; init; } = 64;
    public RandomTimeSpan CheckPeriod { get; init; } = TimeSpan.FromSeconds(5).ToRandom(0.1);
    public RetryDelaySeq RetryDelays { get; init; } = RetryDelaySeq.Exp(0.25, 5);
    public int ConcurrencyLevel { get; init; } = HardwareInfo.GetProcessorCountFactor(4);
    public LogLevel LogLevel { get; init; } = LogLevel.Information;
    public bool IsTracingEnabled { get; init; }
}

/// <summary>
/// Configuration options for operation log readers, extending <see cref="DbLogReaderOptions"/>
/// with a start offset for determining the initial read position.
/// </summary>
public abstract record DbOperationLogReaderOptions : DbLogReaderOptions
{
    public TimeSpan StartOffset { get; init; } = TimeSpan.FromSeconds(3);

    protected DbOperationLogReaderOptions()
    {
        ReprocessPolicy = new RetryPolicy(
            5, // (Re)try count
            TimeSpan.FromSeconds(30),
            RetryDelaySeq.Exp(0.25, 1, 0.1, 2)); // Up to 1 second, 2x longer on each iteration
    }
}

/// <summary>
/// Configuration options for event log readers, extending <see cref="DbLogReaderOptions"/>
/// with event-specific reprocess policies.
/// </summary>
public abstract record DbEventLogReaderOptions : DbLogReaderOptions
{
    protected DbEventLogReaderOptions()
    {
        ReprocessPolicy = new RetryPolicy(
            5, // (Re)try count
            TimeSpan.FromMinutes(5),
            RetryDelaySeq.Exp(0.25, 1, 0.1, 2)); // Up to 1 second, 2x longer on each iteration
    }
}
