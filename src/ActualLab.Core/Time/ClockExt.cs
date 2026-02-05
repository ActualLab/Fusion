using System.Reactive.Linq;

namespace ActualLab.Time;

/// <summary>
/// Extension methods for <see cref="MomentClock"/> providing delay, timer,
/// and interval operations.
/// </summary>
public static class ClockExt
{
    // Delay

    public static Task Delay(this MomentClock clock, Moment dueAt, CancellationToken cancellationToken = default)
        => clock.Delay((dueAt - clock.Now).Positive(), cancellationToken);
    public static Task Delay(this MomentClock clock, long dueInMilliseconds, CancellationToken cancellationToken = default)
    {
        if (dueInMilliseconds == Timeout.Infinite)
            return clock.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        if (dueInMilliseconds < 0)
            throw new ArgumentOutOfRangeException(nameof(dueInMilliseconds));
        return clock.Delay(TimeSpan.FromMilliseconds(dueInMilliseconds), cancellationToken);
    }

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
