using ActualLab.Testing.Collections;

namespace ActualLab.Tests.Time;

[Collection(nameof(TimeSensitiveTests)), Trait("Category", nameof(TimeSensitiveTests))]
public class ServerClockTest(ITestOutputHelper @out) : TestBase(@out)
{
    private static double ToleranceMultiplier => TestRunnerInfo.IsBuildAgent() ? 5 : 1;

    [Fact]
    public void BasicTest()
    {
        var epsilon = TimeSpan.FromMilliseconds(100 * ToleranceMultiplier);
        var cpuClock = MomentClockSet.Default.CpuClock;
        var clock = new ServerClock();

        clock.WhenReady.IsCompleted.Should().BeFalse();
        (clock.Now - cpuClock.Now).Should().BeCloseTo(default, epsilon);

        var offset = TimeSpan.FromSeconds(1);
        clock.Offset = offset;
        clock.WhenReady.IsCompleted.Should().BeTrue();
        (clock.Now - offset - cpuClock.Now).Should().BeCloseTo(default, epsilon);

        offset = TimeSpan.FromSeconds(2);
        clock.Offset = offset;
        clock.WhenReady.IsCompleted.Should().BeTrue();
        (clock.Now - offset - cpuClock.Now).Should().BeCloseTo(default, epsilon);
    }
}
