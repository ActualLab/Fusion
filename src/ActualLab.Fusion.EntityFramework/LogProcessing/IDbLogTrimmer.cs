namespace ActualLab.Fusion.EntityFramework.LogProcessing;

public interface IDbLogTrimmer
{
    DbLogKind LogKind { get; }
}

public record DbLogTrimmerOptions
{
    public TimeSpan MaxEntryAge { get; init; } = TimeSpan.FromDays(1);
#if NET7_0_OR_GREATER
    public int BatchSize { get; init; } = 4096; // .NET 7+ uses ExecuteDeleteAsync
#else
    public int BatchSize { get; init; } = 1024; // .NET 6- deletes rows one-by-one
#endif
    public RandomTimeSpan CheckPeriod { get; init; } = TimeSpan.FromMinutes(15).ToRandom(0.25);
    public RetryDelaySeq RetryDelays { get; init; } = RetryDelaySeq.Exp(TimeSpan.FromSeconds(15), TimeSpan.FromMinutes(10));
    public RandomTimeSpan StatisticsPeriod { get; init; } = TimeSpan.FromHours(1).ToRandom(0.1);
    public LogLevel LogLevel { get; init; } = LogLevel.Information;
    public bool IsTracingEnabled { get; init; }
}
