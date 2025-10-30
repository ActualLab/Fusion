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

        // ComputedOptions tests
        c0.Options.ConsolidationDelay.Should().Be(TimeSpan.Zero);
        c0.Options.TransientErrorInvalidationDelay.Should().Be(TimeSpan.MaxValue);
        c0.Options.InvalidationDelay.Should().Be(TimeSpan.Zero);
        c0.Source.Options.ConsolidationDelay.Should().Be(TimeSpan.MaxValue);
        c0.Source.Options.TransientErrorInvalidationDelay.Should().Be(TimeSpan.FromSeconds(1));

        counterSum[0].Value = 1;
        await (c0.WhenConsolidated ?? Task.CompletedTask);
        c0.IsConsistent().Should().BeFalse();

        var c1 = (ConsolidatingComputed<int>)await c0.Update();
        c1.Value.Should().Be(1);
        c1.WhenConsolidated.Should().BeNull();

        counterSum[0].Invalidate(); // Source dependency invalidation
        await (c1.WhenConsolidated ?? Task.CompletedTask);
        c1.IsConsistent().Should().BeTrue();

        c1.Invalidate(); // Target invalidation
        c1.WhenConsolidated.Should().BeNull();
        c1.IsConsistent().Should().BeFalse();

        var c2 = (ConsolidatingComputed<int>)await c1.Update();
        c2.Value.Should().Be(1);
        c2.IsConsistent().Should().BeTrue();
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

        // ComputedOptions tests
        c0.Options.ConsolidationDelay.Should().Be(TimeSpan.FromSeconds(0.2));
        c0.Options.TransientErrorInvalidationDelay.Should().Be(TimeSpan.MaxValue);
        c0.Options.InvalidationDelay.Should().Be(TimeSpan.Zero);
        c0.Source.Options.ConsolidationDelay.Should().Be(TimeSpan.MaxValue);
        c0.Source.Options.TransientErrorInvalidationDelay.Should().Be(TimeSpan.FromSeconds(1));

        counterSum[0].Value = 1; // Change 1
        var whenConsolidated = c0.WhenConsolidated;
        whenConsolidated.Should().NotBeNull();
        await Task.Delay(MinDelay);
        c0.IsConsistent().Should().BeTrue();

        counterSum[0].Value = 2; // Change 2, within the consolidation window
        await whenConsolidated.WaitAsync(OneSecond);
        c0.IsConsistent().Should().BeFalse();
        c0.WhenConsolidated.Should().BeSameAs(whenConsolidated);

        var c1 = (ConsolidatingComputed<int>)await c0.Update();
        c1.Value.Should().Be(2);
        c1.IsConsistent().Should().BeTrue();

        c1.Source.Invalidate(); // True source invalidation
        whenConsolidated = c1.WhenConsolidated;
        whenConsolidated.Should().NotBeNull();
        await Task.Delay(MinDelay);
        c1.IsConsistent().Should().BeTrue();

        await whenConsolidated.WaitAsync(OneSecond);
        c1.IsConsistent().Should().BeTrue();
        c1.WhenConsolidated.Should().BeNull();

        c1.Invalidate(); // Target invalidation
        c1.WhenConsolidated.Should().BeNull();
        c1.IsConsistent().Should().BeFalse();

        var c2 = (ConsolidatingComputed<int>)await c1.Update();
        c2.Value.Should().Be(2);
        c2.IsConsistent().Should().BeTrue();
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

        counterSum[0].Value = 1; // Change 1
        var whenConsolidated = c0.WhenConsolidated;
        whenConsolidated.Should().NotBeNull();

        await Task.Delay(MinDelay);
        counterSum[0].Value = 0; // Change 2: revert Change 1 within the consolidation window
        c0.WhenConsolidated.Should().BeSameAs(whenConsolidated);

        await whenConsolidated.WaitAsync(OneSecond);
        c0.IsConsistent().Should().BeTrue();
        c0.WhenConsolidated.Should().BeNull();

        counterSum[0].Invalidate(); // Source dependency invalidation
        await c0.WhenConsolidated.WaitAsync(OneSecond);
        c0.IsConsistent().Should().BeTrue();

        c0.Invalidate(); // Target invalidation
        c0.WhenConsolidated.Should().BeNull();
        c0.IsConsistent().Should().BeFalse();

        var c1 = (ConsolidatingComputed<int>)await c0.Update();
        c1.Value.Should().Be(0);
        c1.IsConsistent().Should().BeTrue();
    }

    protected override void ConfigureServices(ServiceCollection services)
    {
        base.ConfigureServices(services);
        services.AddFusion().AddService<CounterSumService>();
    }
}
