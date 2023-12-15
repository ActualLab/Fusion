using System.Reactive.PlatformServices;

namespace ActualLab.Time;

public interface IMomentClock : ISystemClock
{
    Moment Now { get; }

    Moment ToRealTime(Moment localTime);
    Moment ToLocalTime(Moment realTime);
    TimeSpan ToRealDuration(TimeSpan localDuration);
    TimeSpan ToLocalDuration(TimeSpan realDuration);

    Task Delay(TimeSpan dueIn, CancellationToken cancellationToken = default);
}
