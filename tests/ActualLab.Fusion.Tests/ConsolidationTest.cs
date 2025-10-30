using ActualLab.Fusion.Tests.Services;
using ActualLab.Testing.Collections;

namespace ActualLab.Fusion.Tests;

[Collection(nameof(TimeSensitiveTests)), Trait("Category", nameof(TimeSensitiveTests))]
public class ConsolidationTest(ITestOutputHelper @out) : SimpleFusionTestBase(@out)
{
    private static readonly TimeSpan OneSecond = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MinDelay = TimeSpan.FromMilliseconds(50);

    [Fact]
    public async Task NoDelay_InvalidatesImmediately()
    {
        var services = CreateServices();
        var counterSum = services.GetRequiredService<CounterSumService>();
        counterSum[0].Value = 0;

        var c0 = (ConsolidatingComputed<int>)await Computed.Capture(() => counterSum.GetC0(0));
        c0.Value.Should().Be(0);
        c0.WhenConsolidated.Should().BeNull();

        counterSum[0].Value = 1;
        await (c0.WhenConsolidated ?? Task.CompletedTask);
        c0.IsConsistent().Should().BeFalse();

        var c1 = (ConsolidatingComputed<int>)await c0.Update();
        c1.Value.Should().Be(1);
        c1.WhenConsolidated.Should().BeNull();

        counterSum[0].Invalidate();
        await (c0.WhenConsolidated ?? Task.CompletedTask);
        c1.IsConsistent().Should().BeTrue();
    }

    [Fact]
    public async Task Delay_InvalidatesAfterWindowWhenChanged()
    {
        var services = CreateServices();
        var counterSum = services.GetRequiredService<CounterSumService>();
        counterSum[0].Value = 0;

        var c0 = (ConsolidatingComputed<int>)await Computed.Capture(() => counterSum.GetC2(0));
        c0.Value.Should().Be(0);
        c0.WhenConsolidated.Should().BeNull();

        counterSum[0].Value = 1;
        var whenConsolidated = c0.WhenConsolidated;
        whenConsolidated.Should().NotBeNull();
        await Task.Delay(MinDelay);
        c0.IsConsistent().Should().BeTrue();

        await whenConsolidated.WaitAsync(OneSecond);
        c0.IsConsistent().Should().BeFalse();
        c0.WhenConsolidated.Should().BeSameAs(whenConsolidated);

        var c1 = (ConsolidatingComputed<int>)await c0.Update();
        c1.Value.Should().Be(1);
        c1.IsConsistent().Should().BeTrue();

        counterSum[0].Invalidate();
        whenConsolidated = c1.WhenConsolidated;
        whenConsolidated.Should().NotBeNull();
        await Task.Delay(MinDelay);
        c1.IsConsistent().Should().BeTrue();

        await whenConsolidated.WaitAsync(OneSecond);
        c1.IsConsistent().Should().BeTrue();
        c1.WhenConsolidated.Should().BeNull();
    }

    [Fact]
    public async Task Delay_SuppressesFlappingWithinWindow()
    {
        var services = CreateServices();
        var counterSum = services.GetRequiredService<CounterSumService>();
        counterSum[0].Value = 0;

        var c0 = (ConsolidatingComputed<int>)await Computed.Capture(() => counterSum.GetC2(0));
        c0.Value.Should().Be(0);
        c0.WhenConsolidated.Should().BeNull();

        counterSum[0].Value = 1;
        var whenConsolidated = c0.WhenConsolidated;
        whenConsolidated.Should().NotBeNull();

        await Task.Delay(MinDelay);
        counterSum[0].Value = 0;
        c0.WhenConsolidated.Should().BeSameAs(whenConsolidated);

        await whenConsolidated.WaitAsync(OneSecond);
        c0.IsConsistent().Should().BeTrue();
        c0.WhenConsolidated.Should().BeNull();

        counterSum[0].Invalidate();
        await c0.WhenConsolidated.WaitAsync(OneSecond);
        c0.IsConsistent().Should().BeTrue();
    }

    protected override void ConfigureServices(ServiceCollection services)
    {
        base.ConfigureServices(services);
        services.AddFusion().AddService<CounterSumService>();
    }
}
