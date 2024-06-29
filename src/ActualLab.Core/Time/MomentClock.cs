using System.Reactive.PlatformServices;

namespace ActualLab.Time;

public abstract class MomentClock : ISystemClock
{
    public abstract Moment Now { get; }
    public virtual DateTimeOffset UtcNow => Now.ToDateTimeOffset();

    public override string ToString()
        => $"{GetType().Name}()";

    public virtual Moment ToRealTime(Moment localTime) => localTime;
    public virtual Moment ToLocalTime(Moment realTime) => realTime;
    public virtual TimeSpan ToRealDuration(TimeSpan localDuration) => localDuration;
    public virtual TimeSpan ToLocalDuration(TimeSpan realDuration) => realDuration;

    public virtual Task Delay(TimeSpan dueIn, CancellationToken cancellationToken = default)
        => Task.Delay(dueIn, cancellationToken);
}
