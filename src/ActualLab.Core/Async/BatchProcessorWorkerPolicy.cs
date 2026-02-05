using ActualLab.OS;

namespace ActualLab.Async;

/// <summary>
/// Defines the worker scaling policy for a <see cref="BatchProcessor{T, TResult}"/>.
/// </summary>
public interface IBatchProcessorWorkerPolicy
{
    public int MinWorkerCount { get; }
    public int MaxWorkerCount { get; }

    public TimeSpan Cooldown { get; }
    public TimeSpan CollectorCycle { get; }

    public int GetWorkerCountDelta(TimeSpan minQueueTime);
}

/// <summary>
/// Default implementation of <see cref="IBatchProcessorWorkerPolicy"/> with configurable scaling thresholds.
/// </summary>
public record BatchProcessorWorkerPolicy : IBatchProcessorWorkerPolicy
{
    public static IBatchProcessorWorkerPolicy Default { get; set; } = new BatchProcessorWorkerPolicy();
    public static IBatchProcessorWorkerPolicy DbDefault { get; set; } = new BatchProcessorWorkerPolicy() {
        MaxWorkerCount = Math.Max(1, HardwareInfo.GetProcessorCountFactor() / 2),
    };

    public int MinWorkerCount { get; init; } = 1;
    public int MaxWorkerCount { get; init; } = HardwareInfo.GetProcessorCountFactor();

    public TimeSpan KillWorkerAt { get; init; } = TimeSpan.FromMilliseconds(1);
    public TimeSpan Kill8WorkersAt { get; init; } = TimeSpan.FromMilliseconds(0.1);
    public TimeSpan AddWorkerAt { get; init; } = TimeSpan.FromMilliseconds(20);
    public TimeSpan Add4WorkersAt { get; init; } = TimeSpan.FromMilliseconds(100);
    public TimeSpan Add8WorkersAt { get; init; } = TimeSpan.FromMilliseconds(500);

    public TimeSpan Cooldown { get; init; } = TimeSpan.FromMilliseconds(100);
    public TimeSpan CollectorCycle { get; set; } = TimeSpan.FromSeconds(5);

    public virtual int GetWorkerCountDelta(TimeSpan minQueueTime)
    {
        if (minQueueTime > Add8WorkersAt)
            return 8;
        if (minQueueTime > Add4WorkersAt)
            return 4;
        if (minQueueTime > AddWorkerAt)
            return 1;
        if (minQueueTime < Kill8WorkersAt)
            return -8;
        if (minQueueTime < KillWorkerAt)
            return -1;
        return 0;
    }
}
