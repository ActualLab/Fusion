using ActualLab.Fusion.Internal;
using ActualLab.Fusion.Tests.Services;
using ActualLab.Testing.Collections;

namespace ActualLab.Fusion.Tests;

[Collection(nameof(TimeSensitiveTests)), Trait("Category", nameof(TimeSensitiveTests))]
public class CounterServiceTest(ITestOutputHelper @out) : SimpleFusionTestBase(@out)
{
    [Fact]
    public async Task BasicTest()
    {
        var services = CreateServices();
        var counters = services.GetRequiredService<CounterService>();

        var c = Computed.GetExisting(() => counters.Get("a"));
        c.Should().BeNull();

        c = await Computed.Capture(() => counters.Get("a"));
        c.Value.Should().Be(0);
        var c1 = Computed.GetExisting(() => counters.Get("a"));
        c1.Should().BeSameAs(c);

        await counters.Increment("a");
        c.IsConsistent().Should().BeFalse();
        c1 = Computed.GetExisting(() => counters.Get("a"));
        c1.Should().BeNull();
    }

    [Fact]
    public async Task LongWaitTest()
    {
        var services = CreateServices();
        var counters = services.GetRequiredService<CounterService>();

        var key = "a wait";
        var getTask = counters.Get(key);
        await Delay(0.1);

        var c = Computed.GetExisting(() => counters.Get(key));
        c!.ConsistencyState.Should().Be(ConsistencyState.Computing);

        using (Invalidation.Begin())
            _ = counters.Get(key);

        var c1 = Computed.GetExisting(() => counters.Get(key));
        c1.Should().BeSameAs(c);
        c1!.ConsistencyState.Should().Be(ConsistencyState.Computing);

        await getTask;
        await Delay(0.1);
        c.ConsistencyState.Should().Be(ConsistencyState.Invalidated);

        var c2 = Computed.GetExisting(() => counters.Get(key));
        c2.Should().BeNull();
    }

    [Fact]
    public async Task ConcurrentWaitTest()
    {
        var services = CreateServices();
        var counters = services.GetRequiredService<CounterService>();

        // Case 1: Tasks for both keys are started, but just the first one is used
        await counters.Set("x", 1);
        await counters.Set("y wait", 2);
        var sw = new Stopwatch();
        sw.Start();
        var c = await Computed.Capture(() => counters.GetFirstNonZero("x", "y wait"));
        sw.ElapsedMilliseconds.Should().BeLessThan(250);
        c.Value.Should().Be(1);
        ComputedImpl.GetDependencies(c).Length.Should().Be(1);

        // Case 2: both keys are used
        await counters.Set("x", 0);
        await counters.Set("y wait", 2); // Just to make sure it gets recomputed
        sw = new Stopwatch();
        sw.Start();
        c = await Computed.Capture(() => counters.GetFirstNonZero("x", "y wait"));
        sw.ElapsedMilliseconds.Should().BeGreaterThan(250);
        c.Value.Should().Be(2);
        ComputedImpl.GetDependencies(c).Length.Should().Be(2);

        // Case 3: first key throws an error
        await counters.Set("x fail", 0);
        await counters.Set("y wait", 2); // Just to make sure it gets recomputed
        sw = new Stopwatch();
        sw.Start();
        c = await Computed.Capture(() => counters.GetFirstNonZero("x fail", "y wait"));
        sw.ElapsedMilliseconds.Should().BeLessThan(250);
        c.Error!.GetType().Should().Be(typeof(ArgumentOutOfRangeException));
        ComputedImpl.GetDependencies(c).Length.Should().Be(1);
    }

    protected override void ConfigureServices(ServiceCollection services)
    {
        base.ConfigureServices(services);
        services.AddFusion().AddService<CounterService>();
    }
}
