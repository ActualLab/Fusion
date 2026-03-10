namespace ActualLab.Fusion.Tests;

public class VersionStateTest(ITestOutputHelper @out) : SimpleFusionTestBase(@out)
{
    [Fact]
    public void BasicTest()
    {
        var factory = CreateServices().StateFactory();
        var vs = factory.NewVersion();

        vs.Value.Should().Be(0L);

        vs.Increment();
        vs.Value.Should().Be(1L);

        vs.Increment();
        vs.Value.Should().Be(2L);
    }

    [Fact]
    public void CustomInitialValueTest()
    {
        var factory = CreateServices().StateFactory();
        var vs = factory.NewVersion(10L);

        vs.Value.Should().Be(10L);

        vs.Increment();
        vs.Value.Should().Be(11L);
    }

    [Fact]
    public void CategoryTest()
    {
        var factory = CreateServices().StateFactory();
        var vs = factory.NewVersion(category: "test-version");

        vs.Value.Should().Be(0L);
        vs.Category.Should().Be("test-version");
    }

    [Fact]
    public void FactoryNewVersionWithOptionsTest()
    {
        var factory = CreateServices().StateFactory();
        var options = new VersionState.Options {
            InitialValue = 5L,
            Category = "opt-version",
        };
        var vs = factory.NewVersion(options);

        vs.Value.Should().Be(5L);
        vs.Category.Should().Be("opt-version");

        vs.Increment();
        vs.Value.Should().Be(6L);
    }

    [Fact]
    public async Task IncrementInvalidatesComputedTest()
    {
        var factory = CreateServices().StateFactory();
        var vs = factory.NewVersion();

        var computed = vs.Computed;
        computed.Value.Should().Be(0L);
        computed.IsConsistent().Should().BeTrue();

        vs.Increment();

        computed.IsConsistent().Should().BeFalse();
        computed = await computed.Update();
        computed.Value.Should().Be(1L);
        computed.IsConsistent().Should().BeTrue();
    }

    [Fact]
    public async Task IncrementTriggersUpdatedEventTest()
    {
        var factory = CreateServices().StateFactory();
        var vs = factory.NewVersion();
        var tcs = new TaskCompletionSource();

        vs.Updated += (_, _) => tcs.TrySetResult();

        vs.Increment();
        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task UsedAsComputedDependencyTest()
    {
        var factory = CreateServices().StateFactory();
        var vs = factory.NewVersion();

        var cs = factory.NewComputed<string>(
            FixedDelayer.NextTick,
            async ct => {
                var v = await vs.Computed.Use(ct);
                return $"v{v}";
            });
        var c = await cs.Computed.Update();
        c.Value.Should().Be("v0");

        vs.Increment();
        c = await c.Update();
        c.Value.Should().Be("v1");

        vs.Increment();
        c = await c.Update();
        c.Value.Should().Be("v2");
    }

    [Fact]
    public void MultipleIncrementsTest()
    {
        var factory = CreateServices().StateFactory();
        var vs = factory.NewVersion(100L);

        for (var i = 1; i <= 10; i++) {
            vs.Increment();
            vs.Value.Should().Be(100L + i);
        }
    }
}
