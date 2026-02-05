namespace ActualLab.Net;

/// <summary>
/// Represents a computed retry delay including the delay task and when it ends.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct RetryDelay(
    Task Task,
    Moment EndsAt, // Relative to CpuClock.Now
    bool IsLimitExceeded = false)
{
    public static readonly RetryDelay None = new(Task.CompletedTask, default);
    public static readonly RetryDelay LimitExceeded = new(Task.CompletedTask, default, true);

    public override string ToString()
        => $"{nameof(RetryDelay)}(EndsAt = {EndsAt.ToDateTime():T})";

    public static implicit operator RetryDelay((Task DelayTask, Moment EndsAt) source)
        => new(source.DelayTask, source.EndsAt);
}
