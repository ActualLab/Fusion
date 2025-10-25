using ActualLab.Testing.Collections;

namespace ActualLab.Tests.Time;

[Collection(nameof(TimeSensitiveTests)), Trait("Category", nameof(TimeSensitiveTests))]
public class ConcurrentFixedTimerSetTest(ITestOutputHelper @out) : TestBase(@out)
{
    private class Item
    {
        private readonly ThreadSafe<Moment> _firedAt = new();
        public Moment FiredAt {
            get => _firedAt.Value;
            set => _firedAt.Value = value;
        }
    }

    [Fact]
    public async Task BasicTest()
    {
        var clock = MomentClockSet.Default.CpuClock;
        var fireDelay = TimeSpan.FromMilliseconds(250);
        await using var set = new ConcurrentFixedTimerSet<Item>(
            new() {
                Clock = clock,
                TickSource = new TickSource(TimeSpan.FromMilliseconds(20)),
                FireDelay = fireDelay,
            },
            item => item.FiredAt = clock.Now);

        var item1 = new Item();
        var item2 = new Item();
        set.Add(item1);
        set.Add(item2);
        set.Count.Should().Be(2);

        // Before delay nothing should be fired
        await clock.Delay(TimeSpan.FromMilliseconds(50));
        item1.FiredAt.Should().Be(default);
        item2.FiredAt.Should().Be(default);

        // Wait long enough for both to fire
        await clock.Delay(TimeSpan.FromMilliseconds(500));
        item1.FiredAt.Should().NotBe(default);
        item2.FiredAt.Should().NotBe(default);

        await TestExt.When(() => set.Count.Should().Be(0), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task FireImmediatelyTest()
    {
        var clock = MomentClockSet.Default.CpuClock;
        await using var set = new ConcurrentFixedTimerSet<Item>(
            new() {
                Clock = clock,
                TickSource = new TickSource(TimeSpan.FromMilliseconds(20)),
                FireDelay = TimeSpan.FromSeconds(10), // Long delay so items wouldn't fire naturally
            },
            item => item.FiredAt = clock.Now);

        var items = Enumerable.Range(0, 10).Select(_ => new Item()).ToList();
        foreach (var it in items)
            set.Add(it);
        set.Count.Should().Be(items.Count);

        // Fire all now
        await set.FireImmediately();

        foreach (var it in items)
            it.FiredAt.Should().NotBe(default);

        await TestExt.When(() => set.Count.Should().Be(0), TimeSpan.FromSeconds(1));
    }
}
