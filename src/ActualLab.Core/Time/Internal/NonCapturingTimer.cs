namespace ActualLab.Time.Internal;

/// <summary>
/// Creates <see cref="Timer"/> instances without capturing the current
/// <see cref="ExecutionContext"/>, avoiding unintended context flow.
/// </summary>
public static class NonCapturingTimer
{
    public static Timer Create(
        TimerCallback callback,
        object state,
        TimeSpan dueTime,
        TimeSpan period)
    {
        if (callback is null)
            throw new ArgumentNullException(nameof(callback));
        var isFlowSuppressed = false;
        try {
            if (!ExecutionContext.IsFlowSuppressed()) {
                ExecutionContext.SuppressFlow();
                isFlowSuppressed = true;
            }
            return new Timer(callback, state, dueTime, period);
        }
        finally {
            if (isFlowSuppressed)
                ExecutionContext.RestoreFlow();
        }
    }
}
