using ActualLab.CommandR.Operations;
using ActualLab.Fusion.Tests.Services;
using ActualLab.Testing.Collections;

namespace ActualLab.Fusion.Tests;

[Collection(nameof(TimeSensitiveTests)), Trait("Category", nameof(TimeSensitiveTests))]
public class BypassConsolidationTest(ITestOutputHelper @out) : SimpleFusionTestBase(@out)
{
    [Fact]
    public void BypassAddsFlag()
    {
        CallOptions.Invalidate.HasFlag(CallOptions.BypassConsolidation).Should().BeFalse();
        CallOptions.BypassConsolidation.HasFlag(CallOptions.GetExisting).Should().BeFalse();

        using (Consolidation.Bypass()) {
            ComputeContext.Current.CallOptions.Should().Be(CallOptions.BypassConsolidation);
            Invalidation.IsActive.Should().BeFalse();
        }

        var source = new InvalidationSource("test");
        using (Invalidation.Begin(source))
        using (Consolidation.Bypass()) {
            var context = ComputeContext.Current;
            context.CallOptions.Should().Be(CallOptions.Invalidate | CallOptions.BypassConsolidation);
            context.InvalidationSource.Value.Should().Be(source.Value);
            Invalidation.IsActive.Should().BeTrue();
        }
        using (Invalidation.Begin(bypassConsolidation: true, source)) {
            var context = ComputeContext.Current;
            context.CallOptions.Should().Be(CallOptions.Invalidate | CallOptions.BypassConsolidation);
            context.InvalidationSource.Value.Should().Be(source.Value);
        }
        using (Invalidation.Begin(bypassConsolidation: false))
            ComputeContext.Current.CallOptions.Should().Be(CallOptions.Invalidate);

        // Captured invalidations are replayed without the flag, so it isn't added there
        using (new ComputeContext(new List<ServiceCall>()).Activate()) {
            var context = ComputeContext.Current;
            using (Consolidation.Bypass())
                ComputeContext.Current.Should().BeSameAs(context);
        }
    }

    [Fact]
    public async Task CaptureReturnsSource()
    {
        var services = CreateServices();
        var counterSum = services.GetRequiredService<CounterSumService>();
        counterSum[0].Set(1);

        var source = await CaptureSource(counterSum, 0);
        source.Should().NotBeAssignableTo<IConsolidatingComputed>();
        source.Value.Should().Be(1);
        var callCount = counterSum.GetC0CallCount;

        var target = (ConsolidatingComputed<int>)await Computed.Capture(() => counterSum.GetC0(0));
        target.Source.Should().BeSameAs(source);
        counterSum.GetC0CallCount.Should().Be(callCount);

        (await CaptureSource(counterSum, 0)).Should().BeSameAs(source);
    }

    [Fact]
    public async Task CaptureCanBypassConsolidation()
    {
        var services = CreateServices();
        var counterSum = services.GetRequiredService<CounterSumService>();
        counterSum[0].Set(1);

        var target = (ConsolidatingComputed<int>)await Computed.Capture(() => counterSum.GetC0(0));
        var source = await Computed.Capture(bypassConsolidation: true, () => counterSum.GetC0(0));
        source.Should().BeSameAs(target.Source);

        // Bypass() can't be used inside a Capture scope: the computed would be captured into its own context
        await FluentActions.Awaiting(() => Computed.Capture(GetInBypassScope).AsTask())
            .Should().ThrowAsync<InvalidOperationException>();
        return;

        async Task<int> GetInBypassScope() {
            using var _ = Consolidation.Bypass();
            return await counterSum.GetC0(0);
        }
    }

    [Fact]
    public async Task GetExistingReturnsSource()
    {
        var services = CreateServices();
        var counterSum = services.GetRequiredService<CounterSumService>();
        counterSum[0].Set(1);

        (await CaptureExisting(counterSum, 0, useConsolidationSource: true)).Should().BeNull();

        var target = (ConsolidatingComputed<int>)await Computed.Capture(() => counterSum.GetC0(0));
        (await CaptureExisting(counterSum, 0, useConsolidationSource: true)).Should().BeSameAs(target.Source);
        (await CaptureExisting(counterSum, 0, useConsolidationSource: false)).Should().BeSameAs(target);
    }

    [Fact]
    public async Task SourceDependantSeesEveryInvalidation()
    {
        var services = CreateServices();
        var counterSum = services.GetRequiredService<CounterSumService>();
        counterSum[0].Set(1);

        var dependant = await Computed.Capture(() => counterSum.GetC0Source(0));
        dependant.Value.Should().Be(1);
        var target = (ConsolidatingComputed<int>)await Computed.Capture(() => counterSum.GetC0(0));

        // The value stays the same: the target swallows this invalidation, the dependant doesn't
        counterSum[0].Invalidate();
        await (target.WhenConsolidated ?? Task.CompletedTask);
        target.IsConsistent().Should().BeTrue();
        dependant.IsConsistent().Should().BeFalse();
        (await dependant.Update()).Value.Should().Be(1);
    }

    [Fact]
    public async Task IsolatedBypassAddsNoDependency()
    {
        var services = CreateServices();
        var counterSum = services.GetRequiredService<CounterSumService>();
        counterSum[0].Set(1);

        var dependant = await Computed.Capture(() => counterSum.GetC0SourceIsolated(0));
        dependant.Value.Should().Be(1);
        var source = await CaptureSource(counterSum, 0);

        counterSum[0].Set(2);
        source.IsConsistent().Should().BeFalse();
        dependant.IsConsistent().Should().BeTrue();
    }

    [Fact]
    public async Task InvalidationReachesSourceWithoutTarget()
    {
        var services = CreateServices();
        var counterSum = services.GetRequiredService<CounterSumService>();
        counterSum[0].Set(1);

        var source = await CaptureSource(counterSum, 0);
        using (Invalidation.Begin())
            _ = counterSum.GetC0(0);
        source.IsConsistent().Should().BeFalse();
    }

    [Fact]
    public async Task NestedCallsDontUseSource()
    {
        var services = CreateServices();
        var counterSum = services.GetRequiredService<CounterSumService>();
        counterSum[0].Set(1);

        using (Consolidation.Bypass()) {
            (await counterSum.GetViaC0(0)).Should().Be(1);
            (await counterSum.Get0(0)).Should().Be(1); // Ignored by a non-consolidating compute method
        }

        (await CaptureExisting(counterSum, 0, useConsolidationSource: false))
            .Should().BeOfType<ConsolidatingComputed<int>>();
    }

    protected override void ConfigureServices(ServiceCollection services)
    {
        base.ConfigureServices(services);
        services.AddFusion().AddService<CounterSumService>();
    }

    // Private methods

    private static async Task<Computed<int>> CaptureSource(CounterSumService counterSum, int counterIndex)
        => await Computed.Capture(bypassConsolidation: true, () => counterSum.GetC0(counterIndex));

    private static async Task<Computed<int>?> CaptureExisting(
        CounterSumService counterSum, int counterIndex, bool useConsolidationSource)
    {
        var callOptions = CallOptions.Capture | CallOptions.GetExisting;
        if (useConsolidationSource)
            callOptions |= CallOptions.BypassConsolidation;
        var context = new ComputeContext(callOptions);
        using (context.Activate())
            await counterSum.GetC0(counterIndex);
        return context.TryGetCaptured<int>();
    }
}
