using ActualLab.Testing.Collections;

namespace ActualLab.Tests.Time;

[Collection(nameof(TimeSensitiveTests)), Trait("Category", nameof(TimeSensitiveTests))]
public sealed class TimerSetTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact(Timeout = 30_000)]
    public async Task TimerAddedAfterIdleShouldFireOnTime()
    {
        // arrange
        var clock = MomentClockSet.Default.CpuClock;
        var quanta = TimeSpan.FromMilliseconds(50);
        var firedAt = new ConcurrentDictionary<int, Moment>();
        await using var timerSet = new TimerSet<int>(
            new() { Clock = clock, TickSource = new TickSource(quanta) },
            timer => firedAt[timer] = clock.Now);
        var delay = TimeSpan.FromMilliseconds(300);
        var lateness = new List<TimeSpan>();

        // act
        for (var timer = 0; timer < 3; timer++) {
            await Delay(1);
            var dueAt = clock.Now + delay;
            timerSet.AddOrUpdate(timer, dueAt);
            var timerCopy = timer;
            await TestExt.When(() => firedAt.ContainsKey(timerCopy).Should().BeTrue(), TimeSpan.FromSeconds(5));
            lateness.Add(firedAt[timer] - dueAt);
        }

        // assert
        timerSet.Count.Should().Be(0);
        foreach (var late in lateness) {
            late.Should().BeGreaterThan(-quanta.MultiplyBy(1.5),
                "a timer added after the set went idle must not fire early");
            late.Should().BeLessThan(quanta.MultiplyBy(4),
                "a timer added after the set went idle must not fire late");
        }
    }

    [Fact(Timeout = 30_000)]
    public async Task EarlierTimerShouldWakeUpSetSleepingUntilLaterOne()
    {
        // arrange
        var clock = MomentClockSet.Default.CpuClock;
        var quanta = TimeSpan.FromMilliseconds(50);
        var firedAt = new ConcurrentDictionary<int, Moment>();
        await using var timerSet = new TimerSet<int>(
            new() { Clock = clock, TickSource = new TickSource(quanta) },
            timer => firedAt[timer] = clock.Now);
        timerSet.AddOrUpdate(1, clock.Now + TimeSpan.FromSeconds(10));
        await Delay(1);

        // act
        var dueAt = clock.Now + TimeSpan.FromMilliseconds(300);
        timerSet.AddOrUpdate(2, dueAt);
        await TestExt.When(() => firedAt.ContainsKey(2).Should().BeTrue(), TimeSpan.FromSeconds(5));

        // assert
        (firedAt[2] - dueAt).Should().BeLessThan(quanta.MultiplyBy(4),
            "a timer earlier than the one the set sleeps until must wake it up");
        firedAt.ContainsKey(1).Should().BeFalse("the later timer isn't due yet");
        timerSet.Count.Should().Be(1, "the later timer must stay in the set");
    }

    [Fact(Timeout = 30_000)]
    public async Task RemovingEarliestTimerShouldNotDelayNextOne()
    {
        // arrange
        var clock = MomentClockSet.Default.CpuClock;
        var quanta = TimeSpan.FromMilliseconds(50);
        var firedAt = new ConcurrentDictionary<int, Moment>();
        await using var timerSet = new TimerSet<int>(
            new() { Clock = clock, TickSource = new TickSource(quanta) },
            timer => firedAt[timer] = clock.Now);
        var dueAt = clock.Now + TimeSpan.FromMilliseconds(800);
        timerSet.AddOrUpdate(1, clock.Now + TimeSpan.FromMilliseconds(400));
        timerSet.AddOrUpdate(2, dueAt);
        await Delay(0.1);

        // act
        timerSet.Remove(1);
        await TestExt.When(() => firedAt.ContainsKey(2).Should().BeTrue(), TimeSpan.FromSeconds(5));

        // assert
        firedAt.ContainsKey(1).Should().BeFalse("a removed timer must not fire");
        (firedAt[2] - dueAt).Should().BeGreaterThan(-quanta.MultiplyBy(1.5),
            "waking up at the removed timer's time must not fire the next one early");
        (firedAt[2] - dueAt).Should().BeLessThan(quanta.MultiplyBy(4),
            "the set must go back to sleep until the next timer, not past it");
    }

    [Fact(Timeout = 30_000)]
    public async Task SleepingSetShouldNotHoldFiredTimers()
    {
        // arrange
        var clock = MomentClockSet.Default.CpuClock;
        var quanta = TimeSpan.FromMilliseconds(50);
        await using var timerSet = new TimerSet<object>(
            new() { Clock = clock, TickSource = new TickSource(quanta) },
            _ => { });
        var timerRef = AddTimer(timerSet, clock.Now + TimeSpan.FromMilliseconds(100));

        // act
        await TestExt.When(() => timerSet.Count.Should().Be(0), TimeSpan.FromSeconds(5));
        await Delay(0.2);

        // assert
        await TestExt.When(() => {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            timerRef.TryGetTarget(out _).Should().BeFalse("a fired timer must not stay referenced while the set sleeps");
        }, TimeSpan.FromSeconds(5));
    }

    [Fact(Timeout = 30_000)]
    public async Task SleepEndingSlightlyEarlyShouldNotDelayTimerByQuantum()
    {
        // arrange
        var clock = new EarlyWakingClock(TimeSpan.FromMilliseconds(30));
        var quanta = TimeSpan.FromMilliseconds(100);
        var start = clock.Now;
        var firedAt = new ConcurrentDictionary<int, Moment>();
        await using var timerSet = new TimerSet<int>(
            new() { Clock = clock, TickSource = new TickSource(quanta) },
            timer => firedAt[timer] = clock.Now,
            start);
        var lateness = new List<TimeSpan>();

        // act
        for (var timer = 0; timer < 3; timer++) {
            var priority = timerSet.GetPriority(clock.Now) + 5;
            var dueAt = start + quanta.MultiplyBy(priority);
            timerSet.AddOrUpdate(timer, priority);
            var timerCopy = timer;
            await TestExt.When(() => firedAt.ContainsKey(timerCopy).Should().BeTrue(), TimeSpan.FromSeconds(5));
            lateness.Add(firedAt[timer] - dueAt);
        }

        // assert
        foreach (var late in lateness)
            late.Should().BeLessThan(quanta.MultiplyBy(1.5),
                "a sleep that ends slightly before its quantum must not cost an extra quantum");
    }

    // Private methods

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<object> AddTimer(TimerSet<object> timerSet, Moment dueAt)
    {
        var timer = new object();
        timerSet.AddOrUpdate(timer, dueAt);
        return new WeakReference<object>(timer);
    }

    // Nested types

    private sealed class EarlyWakingClock(TimeSpan earliness) : MomentClock
    {
        public override Moment Now => CpuClock.Instance.Now;

        public override Task Delay(TimeSpan dueIn, CancellationToken cancellationToken = default)
            => base.Delay(TimeSpanExt.Max(TimeSpan.Zero, dueIn - earliness), cancellationToken);
    }
}
