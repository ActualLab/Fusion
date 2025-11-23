namespace ActualLab.Time;

public class ServerClock(MomentClock? baseClock = null) : MomentClock
{
    private volatile TaskCompletionSource<TimeSpan> _offsetSource = TaskCompletionSourceExt.New<TimeSpan>();

    public MomentClock BaseClock { get; } = baseClock ?? MomentClockSet.Default.CpuClock;

    public TimeSpan Offset {
        get {
            var offsetTask = _offsetSource.Task;
            return offsetTask.IsCompleted ? offsetTask.GetAwaiter().GetResult() : default;
        }
        set {
            if (_offsetSource.Task.IsCompleted)
                _offsetSource = TaskCompletionSourceExt.New<TimeSpan>().WithResult(value);
            else
                _offsetSource.TrySetResult(value);
        }
    }

    public Task WhenReady => _offsetSource.Task;

    public sealed override Moment Now {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => BaseClock.Now + Offset;
    }
}
