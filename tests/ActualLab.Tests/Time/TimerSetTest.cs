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
            late.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero,
                "a timer added after the set went idle must not fire early");
            late.Should().BeLessThan(TimeSpan.FromSeconds(1),
                "a timer added after the set went idle must not wait for its sleep to end");
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
        (firedAt[2] - dueAt).Should().BeGreaterThanOrEqualTo(TimeSpan.Zero,
            "a timer must not fire early");
        (firedAt[2] - dueAt).Should().BeLessThan(TimeSpan.FromSeconds(1),
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
        var dueAt = clock.Now + TimeSpan.FromMilliseconds(1600);
        timerSet.AddOrUpdate(1, clock.Now + TimeSpan.FromMilliseconds(1000));
        timerSet.AddOrUpdate(2, dueAt);
        await Delay(0.1);

        // act
        timerSet.Remove(1);
        await TestExt.When(() => firedAt.ContainsKey(2).Should().BeTrue(), TimeSpan.FromSeconds(5));

        // assert
        firedAt.ContainsKey(1).Should().BeFalse("a removed timer must not fire");
        (firedAt[2] - dueAt).Should().BeGreaterThanOrEqualTo(TimeSpan.Zero,
            "waking up at the removed timer's time must not fire the next one early");
        (firedAt[2] - dueAt).Should().BeLessThan(TimeSpan.FromSeconds(1),
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
        var quanta = TimeSpan.FromMilliseconds(400);
        var start = clock.Now;
        var firedAt = new ConcurrentDictionary<int, Moment>();
        await using var timerSet = new TimerSet<int>(
            new() { Clock = clock, TickSource = new TickSource(quanta), MaxSleepDuration = TimeSpan.FromMinutes(1) },
            timer => firedAt[timer] = clock.Now,
            start);
        var lateness = new List<TimeSpan>();

        // act
        for (var timer = 0; timer < 3; timer++) {
            var priority = timerSet.GetPriority(clock.Now) + 3;
            var quantumEnd = start + quanta.MultiplyBy(priority + 1);
            timerSet.AddOrUpdate(timer, priority);
            var timerCopy = timer;
            await TestExt.When(() => firedAt.ContainsKey(timerCopy).Should().BeTrue(), TimeSpan.FromSeconds(5));
            lateness.Add(firedAt[timer] - quantumEnd);
        }

        // assert
        foreach (var late in lateness) {
            late.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero, "a timer must not fire before its quantum ends");
            late.Should().BeLessThan(quanta.MultiplyBy(0.5),
                "a sleep that ends slightly before its quantum must not cost an extra quantum");
        }
        // ~1 call per ms of a short tail on Linux/macOS (Task.Delay(1 ms) takes ~1 ms there); spinning costs millions
        clock.DelayCallCount.Should().BeLessThan(2000,
            "waiting for the rest of a quantum must not spin on a Clock.Delay that completes synchronously");
    }

    [Fact(Timeout = 60_000)]
    public async Task SetWakingUpOnSharedTickSourceShouldNotFireEarly()
    {
        // arrange
        var clock = MomentClockSet.Default.CpuClock;
        var quanta = TimeSpan.FromMilliseconds(50);
        var tickSource = new TickSource(quanta);
        var start = clock.Now;
        var busySet = (TimerSet<int>)null!;
        // The busy set keeps the shared TickSource ticking: its timer reschedules itself every quantum
        // ReSharper disable once AccessToModifiedClosure
        busySet = new TimerSet<int>(
            new() { Clock = clock, TickSource = tickSource },
            timer => busySet.AddOrUpdate(timer, busySet.GetPriority(clock.Now) + 1),
            start);
        await using var _ = busySet;
        busySet.AddOrUpdate(0, clock.Now);
        var firedAt = new ConcurrentDictionary<int, Moment>();
        await using var timerSet = new TimerSet<int>(
            new() { Clock = clock, TickSource = tickSource },
            timer => firedAt[timer] = clock.Now,
            start);
        var random = new Random(1);
        var lateness = new List<TimeSpan>();

        // act
        for (var timer = 0; timer < 20; timer++) {
            await Delay(0.1 + 0.1 * random.NextDouble()); // timerSet is empty & sleeps here
            var dueAt = clock.Now + TimeSpan.FromMilliseconds(300) + quanta.MultiplyBy(random.NextDouble());
            var quantumEnd = start + quanta.MultiplyBy(timerSet.GetPriority(dueAt) + 1);
            timerSet.AddOrUpdate(timer, dueAt);
            var timerCopy = timer;
            await TestExt.When(() => firedAt.ContainsKey(timerCopy).Should().BeTrue(), TimeSpan.FromSeconds(5));
            lateness.Add(firedAt[timer] - quantumEnd);
        }

        // assert
        Out.WriteLine($"Lateness after quantum end, ms: {string.Join(", ", lateness.Select(x => x.TotalMilliseconds.ToString("F0")))}");
        foreach (var late in lateness)
            late.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero,
                "a set waking up out of phase with a shared TickSource must not fire before the quantum ends");
    }

    [Fact(Timeout = 30_000)]
    public async Task ServerClockJumpShouldNotDelayTimer()
    {
        // arrange
        var clock = new ServerClock(CpuClock.Instance) { Offset = TimeSpan.Zero };
        var quanta = TimeSpan.FromMilliseconds(50);
        var firedAt = new ConcurrentDictionary<int, Moment>();
        await using var timerSet = new TimerSet<int>(
            new() { Clock = clock, TickSource = new TickSource(quanta), MaxSleepDuration = TimeSpan.Zero },
            timer => firedAt[timer] = clock.Now);
        timerSet.AddOrUpdate(1, clock.Now + TimeSpan.FromSeconds(10));
        await Delay(0.5);

        // act
        var jumpedAt = CpuTimestamp.Now;
        clock.Offset = TimeSpan.FromSeconds(20);
        await TestExt.When(() => firedAt.ContainsKey(1).Should().BeTrue(), TimeSpan.FromSeconds(5));

        // assert
        jumpedAt.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(1),
            "a set that ticks every quantum must notice a forward clock jump right away");
    }

    [Fact(Timeout = 30_000)]
    public async Task MaxSleepDurationShouldBoundClockJumpLateness()
    {
        // arrange
        var clock = new ServerClock(CpuClock.Instance) { Offset = TimeSpan.Zero };
        var quanta = TimeSpan.FromMilliseconds(50);
        var maxSleepDuration = TimeSpan.FromMilliseconds(500);
        var firedAt = new ConcurrentDictionary<int, Moment>();
        await using var timerSet = new TimerSet<int>(
            new() { Clock = clock, TickSource = new TickSource(quanta), MaxSleepDuration = maxSleepDuration },
            timer => firedAt[timer] = clock.Now);
        timerSet.AddOrUpdate(1, clock.Now + TimeSpan.FromSeconds(10));
        await Delay(1); // The set sleeps until the timer, but for at most maxSleepDuration

        // act
        var jumpedAt = CpuTimestamp.Now;
        clock.Offset = TimeSpan.FromSeconds(20);
        await TestExt.When(() => firedAt.ContainsKey(1).Should().BeTrue(), TimeSpan.FromSeconds(5));

        // assert
        jumpedAt.Elapsed.Should().BeLessThan(maxSleepDuration + TimeSpan.FromSeconds(0.7),
            "a forward clock jump must delay a sleeping set's timer by at most MaxSleepDuration");
    }

    [Fact]
    public void MaxSleepDurationShouldBeValidated()
    {
        var create = (TimeSpan value) => new TimerSetOptions { MaxSleepDuration = value };

        new TimerSetOptions().MaxSleepDuration.Should().Be(TimeSpan.FromSeconds(30));
        create(TimeSpan.Zero).MaxSleepDuration.Should().Be(TimeSpan.Zero);
        create(TimerSetOptions.MaxMaxSleepDuration).MaxSleepDuration.Should().Be(TimerSetOptions.MaxMaxSleepDuration);
        create.Invoking(c => c(TimeSpan.FromTicks(-1))).Should().Throw<ArgumentOutOfRangeException>();
        create.Invoking(c => c(TimerSetOptions.MaxMaxSleepDuration + TimeSpan.FromTicks(1)))
            .Should().Throw<ArgumentOutOfRangeException>();
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

    // Its Delay ends early, and completes synchronously when dueIn <= earliness
    private sealed class EarlyWakingClock(TimeSpan earliness) : MomentClock
    {
        private int _delayCallCount;

        public int DelayCallCount => Volatile.Read(ref _delayCallCount);
        public override Moment Now => CpuClock.Instance.Now;

        public override Task Delay(TimeSpan dueIn, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _delayCallCount);
            return base.Delay(TimeSpanExt.Max(TimeSpan.Zero, dueIn - earliness), cancellationToken);
        }
    }
}
