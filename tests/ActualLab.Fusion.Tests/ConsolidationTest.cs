using ActualLab.Fusion.Interception;
using ActualLab.Fusion.Tests.Services;
using ActualLab.Testing.Collections;

namespace ActualLab.Fusion.Tests;

[Collection(nameof(TimeSensitiveTests)), Trait("Category", nameof(TimeSensitiveTests))]
public class ConsolidationTest(ITestOutputHelper @out) : SimpleFusionTestBase(@out)
{
    private static readonly TimeSpan OneSecond = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MinDelay = TimeSpan.FromMilliseconds(50);

    [Fact]
    public async Task MethodInvalidationInvalidatesSourceAndTarget()
    {
        var services = CreateServices();
        var counterSum = services.GetRequiredService<CounterSumService>();
        counterSum[0].Set(0);

        var c0 = (ConsolidatingComputed<int>)await Computed.Capture(() => counterSum.GetC0(0));
        var source0 = c0.Source;
        var invalidated = new List<Computed>();
        source0.Invalidated += invalidated.Add;
        c0.Invalidated += invalidated.Add;

        // The value didn't change, but there is no consolidation: both are invalidated
        using (Invalidation.Begin())
            _ = counterSum.GetC0(0);
        invalidated.Should().Equal(source0, c0);

        // The source invalidation starts a consolidation, but it can't revive the target
        await (c0.WhenConsolidated ?? Task.CompletedTask);
        c0.IsConsistent().Should().BeFalse();
        var c1 = (ConsolidatingComputed<int>)await Computed.Capture(() => counterSum.GetC0(0));
        c1.Should().NotBeSameAs(c0);
        c1.Value.Should().Be(0);
    }

    [Fact]
    public async Task SourceMethodInvalidationConsolidates()
    {
        var services = CreateServices();
        var counterSum = services.GetRequiredService<CounterSumService>();
        counterSum[0].Set(0);

        var c0 = (ConsolidatingComputed<int>)await Computed.Capture(() => counterSum.GetC0(0));
        c0.Value.Should().Be(0);
        var source0 = c0.Source;

        // The value didn't change, so the source is recomputed (hence replaced) and this is swallowed
        using (Invalidation.Begin())
        using (Consolidation.Bypass())
            _ = counterSum.GetC0(0);
        await (c0.WhenConsolidated ?? Task.CompletedTask);
        c0.IsConsistent().Should().BeTrue();
        c0.Source.Should().NotBeSameAs(source0);

        // ... and it propagates once the value really differs
        counterSum[0].Set(1);
        using (Invalidation.Begin())
        using (Consolidation.Bypass())
            _ = counterSum.GetC0(0);
        await (c0.WhenConsolidated ?? Task.CompletedTask);
        c0.IsConsistent().Should().BeFalse();
        (await c0.Update()).Value.Should().Be(1);
    }

    [Fact]
    public async Task TargetInvalidationInvalidatesSource()
    {
        var services = CreateServices();
        var counterSum = services.GetRequiredService<CounterSumService>();
        counterSum[0].Set(0);

        var target = (ConsolidatingComputed<int>)await Computed.Capture(() => counterSum.GetC0(0));
        var source = target.Source;
        var callCount = counterSum.GetC0CallCount;

        target.Invalidate();
        target.IsConsistent().Should().BeFalse();
        source.IsConsistent().Should().BeFalse();

        var refreshed = (ConsolidatingComputed<int>)await Computed.Capture(() => counterSum.GetC0(0));
        refreshed.Value.Should().Be(0);
        refreshed.Source.Should().NotBeSameAs(source);
        counterSum.GetC0CallCount.Should().BeGreaterThan(callCount);
    }

    [Fact]
    public async Task TargetInvalidationRecomputesUntrackedValue()
    {
        var (service, target) = await CaptureUntracked();

        target.Invalidate();
        service.Value = 2;
        (await service.Get()).Should().Be(2);
    }

    [Fact]
    public async Task TargetInvalidationReachesDependants()
    {
        var (service, _) = await CaptureUntracked();
        var outer = await Computed.Capture(() => service.GetOuter());
        outer.Value.Should().Be(1);
        var target = (ConsolidatingComputed<int>)await Computed.Capture(() => service.Get());

        service.Value = 2;
        target.Invalidate();
        outer.IsConsistent().Should().BeFalse();
        (await service.GetOuter()).Should().Be(2);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MethodInvalidationReachesSourceWithoutTarget(bool bypassConsolidation)
    {
        var (service, target) = await CaptureUntracked();

        // The consolidation sees a different value, so it invalidates the target and keeps the new source
        service.Value = 2;
        InvalidateUntracked(service);
        await target.WhenInvalidated().WaitAsync(OneSecond);
        target.IsConsistent().Should().BeFalse();
        Computed.GetExisting(() => service.Get()).Should().BeNull();

        service.Value = 3;
        InvalidateUntracked(service, bypassConsolidation);
        (await service.Get()).Should().Be(3);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(1, false)]
    [InlineData(2, false)]
    public async Task MethodInvalidationDuringConsolidationReachesNewSource(
        int consolidatedValue, bool bypassConsolidation)
    {
        var (service, target) = await CaptureUntracked();

        var (whenGateReached, gate) = service.CloseGate();
        service.Value = consolidatedValue;
        InvalidateUntracked(service);
        var whenConsolidated = target.WhenConsolidated!;
        await whenGateReached.WaitAsync(OneSecond); // The new source has read the value

        service.Value = 3;
        InvalidateUntracked(service, bypassConsolidation);
        if (!bypassConsolidation)
            target.IsConsistent().Should().BeFalse(); // A plain invalidation doesn't wait for the consolidation
        gate.SetResult(default);
        await target.WhenInvalidated().WaitAsync(OneSecond);
        // The new source is invalidated right after its output is set, and a lock-free read in between
        // still sees it consistent - that's the case for any computed invalidated while it's computing
        await whenConsolidated.WaitAsync(OneSecond);
        (await service.Get()).Should().Be(3);
    }

    [Fact]
    public async Task TargetInvalidationDuringConsolidationReachesNewSource()
    {
        var (service, target) = await CaptureUntracked();

        var (whenGateReached, gate) = service.CloseGate();
        InvalidateUntracked(service);
        await whenGateReached.WaitAsync(OneSecond);

        service.Value = 3;
        target.Invalidate();
        gate.SetResult(default);
        await target.WhenInvalidated().WaitAsync(OneSecond);
        (await service.Get()).Should().Be(3);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    public async Task TargetInvalidationBeforeNewSourceAdoptionReachesIt(
        int consolidatedValue, bool useMethodInvalidation)
    {
        var services = CreateServices();
        var service = services.GetRequiredService<UntrackedValueService>();
        service.Value = 1;
        var target = (ConsolidatingComputed<int>)await Computed.Capture(() => service.GetHooked());

        HookedComparer.OnEquals = () => {
            HookedComparer.OnEquals = null;
            service.Value = 3;
            if (useMethodInvalidation) {
                using (Invalidation.Begin())
                    _ = service.GetHooked();
            }
            else
                target.Invalidate();
        };
        try {
            service.Value = consolidatedValue;
            using (Invalidation.Begin())
            using (Consolidation.Bypass())
                _ = service.GetHooked();
            await target.WhenInvalidated().WaitAsync(OneSecond);
        }
        finally {
            HookedComparer.OnEquals = null;
        }
        (await service.GetHooked()).Should().Be(3);
    }

    [Fact]
    public async Task NoDelay_InvalidatesImmediately()
    {
        var services = CreateServices();
        var counterSum = services.GetRequiredService<CounterSumService>();
        counterSum[0].Set(0);

        var c0 = (ConsolidatingComputed<int>)await Computed.Capture(() => counterSum.GetC0(0));
        c0.Value.Should().Be(0);
        c0.WhenConsolidated.Should().BeNull();

        // ComputedOptions tests
        c0.Options.ConsolidationDelay.Should().Be(TimeSpan.Zero);
        c0.Options.TransientErrorInvalidationDelay.Should().Be(TimeSpan.MaxValue);
        c0.Options.InvalidationDelay.Should().Be(TimeSpan.Zero);
        c0.Source.Options.ConsolidationDelay.Should().Be(TimeSpan.MaxValue);
        c0.Source.Options.TransientErrorInvalidationDelay.Should().Be(TimeSpan.FromSeconds(1));

        counterSum[0].Set(1);
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
        counterSum[0].Set(0);

        var c0 = (ConsolidatingComputed<int>)await Computed.Capture(() => counterSum.GetC2(0));
        c0.Value.Should().Be(0);
        c0.WhenConsolidated.Should().BeNull();

        // ComputedOptions tests
        c0.Options.ConsolidationDelay.Should().Be(TimeSpan.FromSeconds(0.2));
        c0.Options.TransientErrorInvalidationDelay.Should().Be(TimeSpan.MaxValue);
        c0.Options.InvalidationDelay.Should().Be(TimeSpan.Zero);
        c0.Source.Options.ConsolidationDelay.Should().Be(TimeSpan.MaxValue);
        c0.Source.Options.TransientErrorInvalidationDelay.Should().Be(TimeSpan.FromSeconds(1));

        counterSum[0].Set(1); // Change 1
        var whenConsolidated = c0.WhenConsolidated;
        whenConsolidated.Should().NotBeNull();
        await Task.Delay(MinDelay);
        c0.IsConsistent().Should().BeTrue();

        counterSum[0].Set(2); // Change 2, within the consolidation window
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
        counterSum[0].Set(0);

        var c0 = (ConsolidatingComputed<int>)await Computed.Capture(() => counterSum.GetC2(0));
        c0.Value.Should().Be(0);
        c0.WhenConsolidated.Should().BeNull();

        counterSum[0].Set(1); // Change 1
        var whenConsolidated = c0.WhenConsolidated;
        whenConsolidated.Should().NotBeNull();

        await Task.Delay(MinDelay);
        counterSum[0].Set(0); // Change 2: revert Change 1 within the consolidation window
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

    [Fact]
    public async Task ConsolidationDoesNotInheritInvalidatorContext()
    {
        var services = CreateServices();
        var counterSum = services.GetRequiredService<CounterSumService>();
        counterSum[0].Set(0);

        var c0 = (ConsolidatingComputed<int>)await Computed.Capture(() => counterSum.GetC0(0));
        c0.Value.Should().Be(0);
        counterSum.LastGetC0Tag.Should().BeNull();
        var callCount = counterSum.GetC0CallCount;

        CounterSumService.AmbientTag.Value = "invalidator";
        counterSum[0].Set(1);
        var whenConsolidated = c0.WhenConsolidated;
        whenConsolidated.Should().NotBeNull();
        CounterSumService.AmbientTag.Value = null;

        await whenConsolidated.WaitAsync(OneSecond);
        // Without this, a consolidation that never recomputed GetC0 would leave the tag null and pass too
        counterSum.GetC0CallCount.Should().BeGreaterThan(callCount);
        counterSum.LastGetC0Tag.Should().BeNull();
    }

    [Fact]
    public async Task ProtectedMethodConsolidates()
    {
        var services = CreateServices();
        var counterSum = services.GetRequiredService<CounterSumService>();
        counterSum[0].Set(0);

        var c0 = (ConsolidatingComputed<int>)await counterSum.CaptureProtectedC0(0);
        c0.Value.Should().Be(0);
        c0.WhenConsolidated.Should().BeNull();
        c0.Options.ConsolidationDelay.Should().Be(TimeSpan.Zero);

        var outer = await Computed.Capture(() => counterSum.GetViaProtectedC0(0));
        outer.Should().BeOfType<ComputeMethodComputed<int>>();
        outer.Value.Should().Be(0);

        counterSum[0].Invalidate(); // Source dependency invalidation, but the value stays the same
        await (c0.WhenConsolidated ?? Task.CompletedTask);
        c0.IsConsistent().Should().BeTrue();
        outer.IsConsistent().Should().BeTrue();

        counterSum[0].Set(1);
        await (c0.WhenConsolidated ?? Task.CompletedTask);
        c0.IsConsistent().Should().BeFalse();
    }

    protected override void ConfigureServices(ServiceCollection services)
    {
        base.ConfigureServices(services);
        services.AddFusion()
            .AddService<CounterSumService>()
            .AddService<UntrackedValueService>();
    }

    private async Task<(UntrackedValueService, ConsolidatingComputed<int>)> CaptureUntracked()
    {
        var services = CreateServices();
        var service = services.GetRequiredService<UntrackedValueService>();
        service.Value = 1;
        var target = (ConsolidatingComputed<int>)await Computed.Capture(() => service.Get());
        target.Value.Should().Be(1);
        return (service, target);
    }

    // bypassConsolidation: true invalidates only the source, so it starts a consolidation
    private static void InvalidateUntracked(UntrackedValueService service, bool bypassConsolidation = true)
    {
        using var invalidating = Invalidation.Begin();
        using var bypassing = bypassConsolidation ? Consolidation.Bypass() : default;
        _ = service.Get();
    }
}
