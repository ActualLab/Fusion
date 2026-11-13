using ActualLab.Testing.Collections;
using ActualLab.Time.Internal;

namespace ActualLab.Tests.Time;

[Collection(nameof(TimeSensitiveTests)), Trait("Category", nameof(TimeSensitiveTests))]
public sealed class CoarseClockHelperTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact(Timeout = 30_000)]
    public async Task TimerShouldStopWhileIdleAndRestartOnRead()
    {
        // arrange
        _ = CoarseClockHelper.Now;

        // act
        var idleStartedAt = CpuTimestamp.Now;
        while (!CoarseClockHelper.IsTimerSuspended && idleStartedAt.Elapsed < TimeSpan.FromSeconds(10))
            await Delay(0.1);
        var isSuspendedWhileIdle = CoarseClockHelper.IsTimerSuspended;
        var firstReadAt = Moment.Now;
        var firstRead = CoarseClockHelper.Now;
        var isSuspendedAfterRead = CoarseClockHelper.IsTimerSuspended;
        await Delay(0.5);
        var secondRead = CoarseClockHelper.Now;

        // assert
        isSuspendedWhileIdle.Should().BeTrue("nobody reads the clock, so its timer must stop");
        isSuspendedAfterRead.Should().BeFalse("a read must restart the timer");
        (firstRead - firstReadAt).Duration().Should().BeLessThan(TimeSpan.FromSeconds(0.1),
            "the first read after the suspension must not return a stale value");
        (secondRead - firstRead).Should().BeGreaterThan(TimeSpan.FromSeconds(0.3),
            "the restarted timer must keep updating the clock");
    }
}
