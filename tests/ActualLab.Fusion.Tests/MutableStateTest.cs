using ActualLab.Fusion.Tests.Services;

namespace ActualLab.Fusion.Tests;

public class MutableStateTest(ITestOutputHelper @out) : SimpleFusionTestBase(@out)
{
    [Fact]
    public async Task BasicTest()
    {
        var factory = CreateServices().StateFactory();

        var ms1 = factory.NewMutable<string>("A");
        ms1.Updated += (s, _) => Out.WriteLine($"ms1 = {((IState<string>)s).ValueOrDefault}");
        ms1.Value.Should().Be("A");

        var ms2 = factory.NewMutable<string>("B");
        ms2.Updated += (s, _)  => Out.WriteLine($"ms2 = {((IState<string>)s).ValueOrDefault}");
        ms2.Value.Should().Be("B");

        var cs = factory.NewComputed<string>(
            FixedDelayer.NextTick,
            async ct => {
                var value1 = await ms1.Computed.Use(ct);
                var value2 = await ms2.Computed.Use(ct);
                return $"{value1}{value2}";
            });
        var c = cs.Computed;
        c = await c.Update();
        c.Value.Should().Be("AB");

        ms1.Value = "X";
        ms1.Value.Should().Be("X");
        c = await c.Update();
        c.Value.Should().Be("XB");

        ms2.Value = "Y";
        ms2.Value.Should().Be("Y");
        c = await c.Update();
        c.Value.Should().Be("XY");

        ms1.Error = new NullReferenceException();
        ms1.HasError.Should().BeTrue();
        ms1.HasValue.Should().BeFalse();
        ms1.Error.Should().BeOfType<NullReferenceException>();
        c = await c.Update();
        c.HasError.Should().BeTrue();
        c.HasValue.Should().BeFalse();
        c.Error.Should().BeOfType<NullReferenceException>();
    }

    [Fact]
    public async Task SkipUpdateWhenEqualTest()
    {
        var factory = CreateServices().StateFactory();
        var o1 = new object();
        var o2 = new object();

        var s = factory.NewMutable<object?>();
        s.Value.Should().Be(null);
        await s.Update();
        var c0 = s.Computed;
        Out.WriteLine($"Computed: {c0}");
        c0.Value.Should().Be(null);

        s.Value = o1;
        s.Value.Should().Be(o1);
        await s.Update();
        var c1 = s.Computed;
        Out.WriteLine($"Computed: {c1}");
        c1.Value.Should().Be(o1);
        c1.Should().NotBe(c0);

        s.Value = o2;
        s.Value.Should().Be(o2);
        await s.Update();
        var c2 = s.Computed;
        Out.WriteLine($"Computed: {c2}");
        c2.Value.Should().Be(o2);
        c2.Should().NotBe(c1);
    }

    [Fact]
    public async Task CounterServiceTest()
    {
        using var stopCts = new CancellationTokenSource();
        var cancellationToken = stopCts.Token;

        async Task Watch<T>(string name, Computed<T> computed)
        {
            while (true) {
                Out.WriteLine($"{name}: {computed.Value}, {computed}");
                await computed.WhenInvalidated(cancellationToken);
                Out.WriteLine($"{name}: {computed.Value}, {computed}");
                computed = await computed.Update(cancellationToken);
            }
        }

        var services = CreateServicesWithComputeService<CounterService>();
        var counters = services.GetRequiredService<CounterService>();
        var aComputed = await Computed.Capture(() => counters.Get("a"));
        _ = Task.Run(() => Watch(nameof(aComputed), aComputed));
        var bComputed = await Computed.Capture(() => counters.Get("b"));
        _ = Task.Run(() => Watch(nameof(bComputed), bComputed));

        await counters.Increment("a");
        await counters.SetOffset(10);

        aComputed = await aComputed.Update();
        aComputed.Value.Should().Be(11);
        aComputed.IsConsistent().Should().BeTrue();

        bComputed = await bComputed.Update();
        bComputed.Value.Should().Be(10);
        bComputed.IsConsistent().Should().BeTrue();

        stopCts.Cancel();
    }

    [Fact]
    public void InitialOutputTest()
    {
        var services = CreateServices();

        var s0 = new MutableState<int>(new MutableState<int>.Options(), services);
        s0.Value.Should().Be(0);
        var s1 = new MutableState<string>(new MutableState<string>.Options(), services);
        s1.Value.Should().Be(null);

        using var s2 = services.StateFactory().NewComputed(async ct => {
            await Task.Delay(100, ct);
            return 1;
        });
        s2.Value.Should().Be(0);
    }
}
