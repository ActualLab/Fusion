using ActualLab.Testing.Collections;
using ActualLab.Time.Testing;

namespace ActualLab.Tests.Time;

[Collection(nameof(TimeSensitiveTests)), Trait("Category", nameof(TimeSensitiveTests))]
public class TestClockTest(ITestOutputHelper @out) : TestBase(@out)
{
    private static readonly TimeSpan Epsilon = TimeSpan.FromMilliseconds(100);

    [Fact]
    public void ConstructorTest()
    {
        // Default constructor
        using var clock1 = new TestClock();
        clock1.Settings.LocalOffset.Should().Be(TimeSpan.Zero);
        clock1.Settings.RealOffset.Should().Be(TimeSpan.Zero);
        clock1.Settings.Multiplier.Should().Be(1);

        // Constructor with parameters
        var localOffset = TimeSpan.FromHours(5);
        var realOffset = TimeSpan.FromMinutes(30);
        var multiplier = 2.0;
        using var clock2 = new TestClock(localOffset, realOffset, multiplier);
        clock2.Settings.LocalOffset.Should().Be(localOffset);
        clock2.Settings.RealOffset.Should().Be(realOffset);
        clock2.Settings.Multiplier.Should().Be(multiplier);

        // Constructor with settings
        var settings = new TestClockSettings(localOffset, realOffset, multiplier);
        using var clock3 = new TestClock(settings);
        clock3.Settings.Should().BeSameAs(settings);
    }

    [Fact]
    public void NowPropertyTest()
    {
        using var clock = new TestClock();
        var now = clock.Now;
        var systemNow = Moment.Now;
        (systemNow - now).Should().BeLessThan(Epsilon);

        // With offset
        using var clockWithOffset = new TestClock(TimeSpan.FromHours(1));
        var offsetNow = clockWithOffset.Now;
        (offsetNow - systemNow - TimeSpan.FromHours(1)).Should().BeLessThan(Epsilon);
    }

    [Fact]
    public void TimeConversionTest()
    {
        var localOffset = TimeSpan.FromHours(2);
        using var clock = new TestClock(localOffset);

        var realTime = Moment.Now;
        var localTime = clock.ToLocalTime(realTime);
        (localTime - realTime - localOffset).Should().BeLessThan(Epsilon);

        var convertedBack = clock.ToRealTime(localTime);
        (convertedBack - realTime).Should().BeLessThan(Epsilon);
    }

    [Fact]
    public void DurationConversionTest()
    {
        using var clock = new TestClock(TimeSpan.Zero, TimeSpan.Zero, 2.0);

        var realDuration = TimeSpan.FromMinutes(10);
        var localDuration = clock.ToLocalDuration(realDuration);
        localDuration.Should().Be(TimeSpan.FromMinutes(20));

        var convertedBack = clock.ToRealDuration(localDuration);
        convertedBack.Should().Be(realDuration);
    }

    [Fact]
    public void SetToTest()
    {
        using var clock = new TestClock();
        var targetMoment = new Moment(new DateTime(2024, 6, 15, 12, 0, 0, DateTimeKind.Utc));

        clock.SetTo(targetMoment);

        var clockNow = clock.Now;
        (clockNow - targetMoment).Should().BeLessThan(Epsilon);
    }

    [Fact]
    public void OffsetByTest()
    {
        using var clock = new TestClock();
        var initialNow = clock.Now;

        clock.OffsetBy(TimeSpan.FromHours(3));
        var afterOffset = clock.Now;
        (afterOffset - initialNow - TimeSpan.FromHours(3)).Should().BeLessThan(Epsilon);

        // Test with milliseconds overload
        clock.OffsetBy(1000);
        var afterMillisOffset = clock.Now;
        (afterMillisOffset - afterOffset - TimeSpan.FromSeconds(1)).Should().BeLessThan(Epsilon);
    }

    [Fact]
    public void SpeedupByTest()
    {
        using var clock = new TestClock();
        var initialNow = clock.Now;

        clock.SpeedupBy(2.0);
        clock.Settings.Multiplier.Should().Be(2.0);

        // The local time should still be close to what it was
        // (SpeedupBy preserves current local time)
        var afterSpeedup = clock.Now;
        (afterSpeedup - initialNow).Should().BeLessThan(Epsilon);
    }

    [Fact]
    public void SettingsChangeTest()
    {
        using var clock = new TestClock();
        var initialSettings = clock.Settings;

        var newSettings = new TestClockSettings(TimeSpan.FromHours(1));
        clock.Settings = newSettings;

        clock.Settings.Should().BeSameAs(newSettings);
        initialSettings.IsUsable.Should().BeFalse();
    }

    [Fact]
    public void SettingsRejectUsedTest()
    {
        using var clock = new TestClock();
        var settings = clock.Settings;

        // Mark settings as used by changing them
        clock.Settings = new TestClockSettings();

        // Trying to use the old settings should throw
        var action = () => clock.Settings = settings;
        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ToStringTest()
    {
        using var clock = new TestClock(TimeSpan.FromHours(1), TimeSpan.FromMinutes(30), 2.0);
        var str = clock.ToString();

        str.Should().Contain("TestClock");
        str.Should().Contain("01:00:00"); // LocalOffset
        str.Should().Contain("2"); // Multiplier
    }

    [Fact]
    public async Task DelayTest()
    {
        using var clock = new TestClock().SpeedupBy(10);
        var startedAt = CpuTimestamp.Now;

        await clock.Delay(TimeSpan.FromSeconds(1));

        var elapsed = startedAt.Elapsed;
        // With 10x speedup, 1 second local time = 0.1 seconds real time
        elapsed.Should().BeGreaterThan(TimeSpan.FromMilliseconds(50));
        elapsed.Should().BeLessThan(TimeSpan.FromMilliseconds(500));
    }

    [Fact]
    public async Task DelayWithCancellationTest()
    {
        using var clock = new TestClock();
        using var cts = new CancellationTokenSource(50);

        var action = async () => await clock.Delay(TimeSpan.FromSeconds(10), cts.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task DelayWithSettingsChangeTest()
    {
        using var clock = new TestClock();
        var startedAt = CpuTimestamp.Now;

        // Start a delay and change settings during it
        var delayTask = clock.Delay(TimeSpan.FromSeconds(10));

        // Wait a bit then speed up the clock significantly
        await Task.Delay(50);
        clock.SpeedupBy(1000);

        await delayTask;

        // Should complete much faster than 10 seconds
        var elapsed = startedAt.Elapsed;
        elapsed.Should().BeLessThan(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task DelayNegativeThrowsTest()
    {
        using var clock = new TestClock();

        // Note: -1 ms equals Timeout.InfiniteTimeSpan, so we use -2 ms
        var action = async () => await clock.Delay(TimeSpan.FromMilliseconds(-2));

        await action.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task DelayInfiniteTest()
    {
        using var clock = new TestClock();
        using var cts = new CancellationTokenSource(100);

        var action = async () => await clock.Delay(Timeout.InfiniteTimeSpan, cts.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
    }
}
