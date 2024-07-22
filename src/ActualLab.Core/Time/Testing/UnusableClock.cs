using ActualLab.Time.Internal;

namespace ActualLab.Time.Testing;

public sealed class UnusableClock : MomentClock
{
    public static readonly UnusableClock Instance = new();

    public override Moment Now => throw Errors.UnusableClock();

    public override Moment ToRealTime(Moment localTime) => throw Errors.UnusableClock();
    public override Moment ToLocalTime(Moment realTime) => throw Errors.UnusableClock();
    public override TimeSpan ToRealDuration(TimeSpan localDuration) => throw Errors.UnusableClock();
    public override TimeSpan ToLocalDuration(TimeSpan realDuration) => throw Errors.UnusableClock();

    public override Task Delay(TimeSpan dueIn, CancellationToken cancellationToken = default)
        => throw Errors.UnusableClock();

    private UnusableClock() { }
}
