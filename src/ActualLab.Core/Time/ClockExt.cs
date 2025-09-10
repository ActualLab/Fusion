using System.Reactive.Linq;

namespace ActualLab.Time;

public static class ClockExt
{
    // Delay

    public static Task Delay(this MomentClock clock, Moment dueAt, CancellationToken cancellationToken = default)
        => clock.Delay((dueAt - clock.Now).Positive(), cancellationToken);
    public static Task Delay(this MomentClock clock, long dueInMilliseconds, CancellationToken cancellationToken = default)
    {
        if (dueInMilliseconds == System.Threading.Timeout.Infinite)
            return clock.Delay(System.Threading.Timeout.InfiniteTimeSpan, cancellationToken);
        if (dueInMilliseconds < 0)
            throw new ArgumentOutOfRangeException(nameof(dueInMilliseconds));
        return clock.Delay(TimeSpan.FromMilliseconds(dueInMilliseconds), cancellationToken);
    }

    // Timeout

    public static ClockTimeout Timeout(this MomentClock clock, TimeSpan duration)
        => new (clock, duration);
    public static ClockTimeout Timeout(this MomentClock clock, double duration)
        => new (clock, TimeSpan.FromSeconds(duration));
    public static ClockTimeout Timeout(this MomentClockSet clocks, TimeSpan duration)
        => new (clocks.CpuClock, duration);
    public static ClockTimeout Timeout(this MomentClockSet clocks, double duration)
        => new (clocks.CpuClock, TimeSpan.FromSeconds(duration));

    // Timer

    public static IObservable<long> Timer(this MomentClock clock, long delayInMilliseconds)
        => clock.Timer(TimeSpan.FromMilliseconds(delayInMilliseconds));
    public static IObservable<long> Timer(this MomentClock clock, TimeSpan dueIn)
    {
        if (clock is SystemClock)
            return Observable.Timer(dueIn); // Perf. optimization
        return Observable.Create<long>(async observer => {
            var completed = false;
            try {
                await clock.Delay(dueIn).ConfigureAwait(false);
                observer.OnNext(0);
                completed = true;
                observer.OnCompleted();
            }
            catch (Exception e) {
                if (!completed)
                    observer.OnError(e);
            }
        });
    }

    // Interval

    public static IObservable<long> Interval(this MomentClock clock, long intervalInMilliseconds)
        => clock.Interval(TimeSpan.FromMilliseconds(intervalInMilliseconds));
    public static IObservable<long> Interval(this MomentClock clock, TimeSpan interval)
        => clock is SystemClock
            ? Observable.Interval(interval) // Perf. optimization
            : clock.Interval(Intervals.Fixed(interval));
    public static IObservable<long> Interval(this MomentClock clock, IEnumerable<TimeSpan> intervals)
    {
        var e = intervals.GetEnumerator();
        return Observable.Create<long>(async (observer, ct) => {
            var completed = false;
            try {
                var index = 0L;
                while (e.MoveNext()) {
                    var dueAt = clock.Now + e.Current;
                    await clock.Delay(dueAt, ct).SuppressCancellationAwait();
                    if (ct.IsCancellationRequested)
                        break;
                    observer.OnNext(index++);
                }
                completed = true;
                observer.OnCompleted();
            }
            catch (Exception e) {
                if (!completed)
                    observer.OnError(e);
            }
            finally {
                e.Dispose();
            }
        });
    }
}
