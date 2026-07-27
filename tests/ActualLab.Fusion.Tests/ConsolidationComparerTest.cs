using ActualLab.Fusion.Tests.Services;
using ActualLab.Testing.Collections;

namespace ActualLab.Fusion.Tests;

[Collection(nameof(TimeSensitiveTests)), Trait("Category", nameof(TimeSensitiveTests))]
public class ConsolidationComparerTest(ITestOutputHelper @out) : SimpleFusionTestBase(@out)
{
    [Fact]
    public async Task CustomComparerLoosensEquality()
    {
        var services = CreateServices();
        var svc = services.GetRequiredService<ConsolidationComparerService>();
        svc.LooseSource.Set(1);

        var c0 = (ConsolidatingComputed<int>)await Computed.Capture(() => svc.GetLoose());
        c0.Value.Should().Be(1);

        svc.LooseSource.Set(3); // Different value, but the same parity
        await (c0.WhenConsolidated ?? Task.CompletedTask);
        c0.IsConsistent().Should().BeTrue();
        c0.Value.Should().Be(1);

        svc.LooseSource.Set(2); // Different parity
        await (c0.WhenConsolidated ?? Task.CompletedTask);
        c0.IsConsistent().Should().BeFalse();

        var c1 = (ConsolidatingComputed<int>)await c0.Update();
        c1.Value.Should().Be(2);
    }

    [Fact]
    public async Task CustomComparerTightensEquality()
    {
        var services = CreateServices();
        var svc = services.GetRequiredService<ConsolidationComparerService>();
        svc.TightSource.Set(1);

        var cDefault = (ConsolidatingComputed<int>)await Computed.Capture(
            () => svc.GetTightWithDefaultComparer());
        var cTight = (ConsolidatingComputed<int>)await Computed.Capture(() => svc.GetTight());
        cDefault.Value.Should().Be(1);
        cTight.Value.Should().Be(1);

        var callCount = NeverEqualComparer.CallCount;
        svc.TightSource.Invalidate(); // The value stays the same
        await (cDefault.WhenConsolidated ?? Task.CompletedTask);
        await (cTight.WhenConsolidated ?? Task.CompletedTask);

        cDefault.IsConsistent().Should().BeTrue(); // Swallowed by the default comparer
        cTight.IsConsistent().Should().BeFalse(); // But not by the custom one
        NeverEqualComparer.CallCount.Should().BeGreaterThan(callCount);
    }

    [Fact]
    public async Task CustomComparerHandlesReferentialEqualityAndNulls()
    {
        var services = CreateServices();
        var svc = services.GetRequiredService<ConsolidationComparerService>();
        svc.BoxSource.Set(1);

        var cDefault = (ConsolidatingComputed<ValueBox?>)await Computed.Capture(
            () => svc.GetBoxWithDefaultComparer());
        var cBox = (ConsolidatingComputed<ValueBox?>)await Computed.Capture(() => svc.GetBox());
        cBox.Value!.Value.Should().Be(1);

        svc.BoxSource.Invalidate(); // A fresh ValueBox instance with the same value is produced
        await (cDefault.WhenConsolidated ?? Task.CompletedTask);
        await (cBox.WhenConsolidated ?? Task.CompletedTask);
        cDefault.IsConsistent().Should().BeFalse(); // Referential equality never consolidates
        cBox.IsConsistent().Should().BeTrue();

        svc.BoxSource.Set(-1); // null
        await (cBox.WhenConsolidated ?? Task.CompletedTask);
        cBox.IsConsistent().Should().BeFalse();

        var cNull = (ConsolidatingComputed<ValueBox?>)await cBox.Update();
        cNull.Value.Should().BeNull();

        svc.BoxSource.Invalidate(); // null vs. null
        await (cNull.WhenConsolidated ?? Task.CompletedTask);
        cNull.IsConsistent().Should().BeTrue();
    }

    [Fact]
    public async Task ErrorsUseDefaultComparer()
    {
        var services = CreateServices();
        var svc = services.GetRequiredService<ConsolidationComparerService>();
        svc.ErrorSource.Set(1);

        var c0 = (ConsolidatingComputed<int>)(await Computed.TryCapture(() => svc.GetFailing()))!;
        c0.Error.Should().BeOfType<InvalidOperationException>();

        var callCount = NeverEqualComparer.CallCount;
        svc.ErrorSource.Invalidate(); // The same error is produced again
        await (c0.WhenConsolidated ?? Task.CompletedTask);

        // NeverEqualComparer would have invalidated c0 if it were consulted
        c0.IsConsistent().Should().BeTrue();
        NeverEqualComparer.CallCount.Should().Be(callCount);
    }

    [Fact]
    public async Task ComparerIsConstructedOnce()
    {
        var services = CreateServices();
        var svc = services.GetRequiredService<ConsolidationComparerService>();
        svc.BoxSource.Set(1);

        var c0 = (ConsolidatingComputed<ValueBox?>)await Computed.Capture(() => svc.GetBox());
        for (var i = 0; i < 5; i++) {
            svc.BoxSource.Invalidate();
            await (c0.WhenConsolidated ?? Task.CompletedTask);
        }
        ValueBoxComparer.InstanceCount.Should().Be(1);
    }

    [Fact]
    public void WrongValueTypeComparerIsRejected()
    {
        var services = CreateServices(s => s.AddFusion().AddComputeService<WrongValueTypeComparerService>());
        var act = () => services.GetRequiredService<WrongValueTypeComparerService>();
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*must implement IEqualityComparer<String>*");
    }

    [Fact]
    public void ComparerWithoutParameterlessCtorIsRejected()
    {
        var services = CreateServices(s => s.AddFusion().AddComputeService<NoParameterlessCtorComparerService>());
        var act = () => services.GetRequiredService<NoParameterlessCtorComparerService>();
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*public parameterless constructor*");
    }

    [Fact]
    public void ComparerWithoutConsolidationDelayIsRejected()
    {
        var services = CreateServices(s => s.AddFusion().AddComputeService<ComparerWithoutDelayService>());
        var act = () => services.GetRequiredService<ComparerWithoutDelayService>();
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*ConsolidationDelay isn't*");
    }

    [Fact]
    public void ComparerOnRemoteComputeMethodIsRejected()
    {
        // This one is rejected by ComputedOptions.Get, i.e. when the service proxy is built
        var act = () => CreateServices(s => s.AddFusion().AddClient<IRemoteComparerCounter>())
            .GetRequiredService<IRemoteComparerCounter>();
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*ConsolidationComparer cannot be used with RemoteComputeMethodAttribute*");
    }

    [Fact]
    public void ComparerOnDistributedServiceMethodIsRejected()
    {
        var fusion = new ServiceCollection().AddFusion();
        var act = () => fusion.AddDistributedService<IRpcComparerCounter, RpcComparerCounter>();
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*ConsolidationDelay cannot be used on*Distributed service*");
    }

    [Fact]
    public void ComparerOnServerAndClientMethodIsAccepted()
    {
        var fusion = new ServiceCollection().AddFusion();
        var act = () => fusion.AddServerAndClient<IRpcComparerCounter, RpcComparerCounter>();
        act.Should().NotThrow();
    }

    protected override void ConfigureServices(ServiceCollection services)
    {
        base.ConfigureServices(services);
        services.AddFusion().AddService<ConsolidationComparerService>();
    }
}
