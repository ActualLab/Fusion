using Microsoft.Extensions.DependencyInjection.Extensions;
using ActualLab.CommandR.Operations;
using ActualLab.Fusion.Operations;
using ActualLab.Fusion.Operations.Internal;
using ActualLab.Fusion.Tests.Services;
using ActualLab.Reflection;
using ActualLab.Serialization;

namespace ActualLab.Fusion.Tests;

public class InvalidationModeTest(ITestOutputHelper @out) : SimpleFusionTestBase(@out)
{
    [Fact]
    public async Task Local_InvalidatesInProcessWithoutReplay()
    {
        var services = CreateHostServices<LocalInvalidationModeService>();
        var kv = services.GetRequiredService<LocalInvalidationModeService>();

        var (cGet, cCount, cLength) = await Capture(kv, "a");
        await services.Commander().Call(new InvalidationModeService_Set("a", 1));

        cGet.IsConsistent().Should().BeFalse();
        cCount.IsConsistent().Should().BeFalse();
        cLength.IsConsistent().Should().BeFalse();
        (await kv.Get("a")).Should().Be(1);
        // A deferred-mode handler has no "if (Invalidation.IsActive)" guard, so a replay
        // would run its mutation a second time
        kv.MutationCount.Should().Be(1);
    }

    [Fact]
    public async Task Local_RecordsNothingOnTheOperation()
    {
        var services = CreateHostServices<LocalInvalidationModeService>();
        await services.Commander().Call(new InvalidationModeService_Set("a", 1));

        var operation = services.GetRequiredService<OperationCapture>().Operations.Single();
        operation.InvalidationCalls.Should().BeEmpty();
    }

    // Replicated is only as reliable as its carrier, so it's rejected unless the operation is stored -
    // see InvalidationModeDbTest for the path that works. Both cases below fail per call, not at
    // startup, because the mode is only known once a handler actually defers something.

    [Fact]
    public async Task Replicated_WithATransientOperationThrows()
    {
        var services = CreateHostServices<TransientReplicatedInvalidationModeService>();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => services.Commander().Call(new InvalidationModeService_Set("a", 1)));
    }

    [Fact]
    public async Task Replicated_WithoutAnOperationThrows()
    {
        var services = CreateHostServices<ScopelessReplicatedInvalidationModeService>();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => services.Commander().Call(new InvalidationModeService_Set("a", 1)));
    }

    [Fact]
    public async Task RecordedCalls_ApplyThroughTheServiceProxy()
    {
        var services = CreateHostServices<LocalInvalidationModeService>();
        var kv = services.GetRequiredService<LocalInvalidationModeService>();
        var serviceType = new TypeRef(typeof(LocalInvalidationModeService));
        var calls = ImmutableList.Create(
            new InvalidationCall(serviceType, "Get", ["ab"]),
            new InvalidationCall(serviceType, "Count", []),
            // A long, not an int: text serialization erases the distinction, so the applier
            // has to bring the argument back to the parameter's type
            new InvalidationCall(serviceType, "CountOfLength", [2L]));

        var (cGet, cCount, cLength) = await Capture(kv, "ab");
        var cOther = await Computed.Capture(() => kv.Get("other"));

        await services.GetRequiredService<InvalidationCallApplier>()
            .Apply(calls, new InvalidationSource("test"));

        cGet.IsConsistent().Should().BeFalse();
        cCount.IsConsistent().Should().BeFalse();
        cLength.IsConsistent().Should().BeFalse();
        cOther.IsConsistent().Should().BeTrue();
    }

    [Fact]
    public async Task RecordedCalls_SurviveTheOperationLogSerializer()
    {
        var services = CreateHostServices<LocalInvalidationModeService>();
        var kv = services.GetRequiredService<LocalInvalidationModeService>();
        var serviceType = new TypeRef(typeof(LocalInvalidationModeService));
        var calls = ImmutableList.Create(
            new InvalidationCall(serviceType, "Get", ["ab"]),
            new InvalidationCall(serviceType, "CountOfLength", [2]));

        var json = NewtonsoftJsonSerializer.Default.Write(calls);
        var restored = NewtonsoftJsonSerializer.Default.Read<ImmutableList<InvalidationCall>>(json);

        restored.Select(x => x.MethodName).Should().Equal("Get", "CountOfLength");
        restored[0].Arguments.Should().Equal("ab");
        var (cGet, _, cLength) = await Capture(kv, "ab");
        await services.GetRequiredService<InvalidationCallApplier>()
            .Apply(restored, new InvalidationSource("test"));

        cGet.IsConsistent().Should().BeFalse();
        cLength.IsConsistent().Should().BeFalse();
    }

    [Fact]
    public async Task Legacy_KeepsReplayingTheCommand()
    {
        var services = CreateHostServices<LegacyInvalidationModeService>();
        var kv = services.GetRequiredService<LegacyInvalidationModeService>();

        var (cGet, cCount, cLength) = await Capture(kv, "a");
        await services.Commander().Call(new InvalidationModeService_Set("a", 1));

        // Legacy invalidation can only happen through the replay pass
        cGet.IsConsistent().Should().BeFalse();
        cCount.IsConsistent().Should().BeFalse();
        cLength.IsConsistent().Should().BeFalse();
        kv.MutationCount.Should().Be(1);
    }

    [Fact]
    public async Task None_InvalidatesNothingAndIsNotReplayed()
    {
        var services = CreateHostServices<NoneInvalidationModeService>();
        var kv = services.GetRequiredService<NoneInvalidationModeService>();

        var (cGet, cCount, cLength) = await Capture(kv, "a");
        await services.Commander().Call(new InvalidationModeService_Set("a", 1));

        cGet.IsConsistent().Should().BeTrue();
        cCount.IsConsistent().Should().BeTrue();
        cLength.IsConsistent().Should().BeTrue();
        kv.MutationCount.Should().Be(1);
    }

    [Fact]
    public async Task None_NestedDeferredHandlerStillInvalidates()
    {
        var services = CreateHostServices<NoneInvalidationModeService>();
        var kv = services.GetRequiredService<NoneInvalidationModeService>();

        var (cGet, cCount, cLength) = await Capture(kv, "a");
        await services.Commander().Call(new InvalidationModeService_SetViaNested("a", 1));

        cGet.IsConsistent().Should().BeFalse();
        cCount.IsConsistent().Should().BeFalse();
        cLength.IsConsistent().Should().BeFalse();
        kv.MutationCount.Should().Be(1);
    }

    [Fact]
    public void Defer_OutsideOfScope_Throws()
        => Assert.Throws<InvalidOperationException>(() => Invalidation.Defer(() => { }));

    [Fact]
    public void Defer_InsideInvalidationPass_Throws()
    {
        using var _1 = Invalidation.BeginDeferred();
        using var _2 = Invalidation.Begin();
        Assert.Throws<InvalidOperationException>(() => Invalidation.Defer(() => { }));
    }

    [Fact]
    public void BeginDeferred_InsideInvalidationPass_Throws()
    {
        using var _ = Invalidation.Begin();
        Assert.Throws<InvalidOperationException>(() => Invalidation.BeginDeferred());
    }

    [Fact]
    public async Task Defer_FromLegacyHandler_Throws()
    {
        var services = CreateHostServices<MisdeclaredInvalidationModeService>();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => services.Commander().Call(new InvalidationModeService_Set("a", 1)));
    }

    [Fact]
    public async Task BeginDeferred_RunsBlocksAtScopeExit()
    {
        var services = CreateHostServices<LocalInvalidationModeService>();
        var kv = services.GetRequiredService<LocalInvalidationModeService>();
        var cGet = await Computed.Capture(() => kv.Get("a"));

        using (Invalidation.BeginDeferred()) {
            Invalidation.Defer(() => _ = kv.Get("a", default));
            cGet.IsConsistent().Should().BeTrue();
        }
        cGet.IsConsistent().Should().BeFalse();
    }

    [Fact]
    public async Task Configuration_MovesLocalServiceToReplicated()
    {
        // The service runs on a transient operation, so the override taking effect is exactly what
        // makes it fail - see InvalidationModeDbTest for the same override on a stored operation
        var services = CreateHostServices<LocalInvalidationModeService>(
            fusion => fusion.WithInvalidationMode<LocalInvalidationModeService>(InvalidationMode.Replicated));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => services.Commander().Call(new InvalidationModeService_Set("a", 1)));
    }

    [Fact]
    public void Configuration_CannotMoveALegacyServiceToADeferredMode()
    {
        var services = CreateHostServices<LegacyInvalidationModeService>(
            fusion => fusion.WithInvalidationMode<LegacyInvalidationModeService>(InvalidationMode.Replicated));
        var resolver = services.GetRequiredService<InvalidationModeResolver>();
        var method = typeof(LegacyInvalidationModeService)
            .GetMethod(nameof(LegacyInvalidationModeService.OnSet))!;

        Assert.Throws<InvalidOperationException>(
            () => resolver.Resolve(typeof(LegacyInvalidationModeService), method));
    }

    [Fact]
    public void Configuration_CannotOverrideToANonDeferredMode()
    {
        var services = CreateHostServices<LocalInvalidationModeService>();
        var resolver = services.GetRequiredService<InvalidationModeResolver>();

        Assert.Throws<InvalidOperationException>(
            () => resolver.Override(typeof(LocalInvalidationModeService), InvalidationMode.Legacy));
    }

    [Fact]
    public void DefaultMode_IsLegacy()
    {
        var services = CreateHostServices<LegacyInvalidationModeService>();
        services.GetRequiredService<InvalidationModeResolver>().DefaultMode
            .Should().Be(InvalidationMode.Legacy);
    }

    // Private methods

    private IServiceProvider CreateHostServices<TService>(Action<FusionBuilder>? configureFusion = null)
        where TService : class, IComputeService
        => CreateServices(services => {
            var fusion = services.AddFusion();
            fusion.AddService<TService>();
            configureFusion?.Invoke(fusion);
            services.AddSingleton<OperationCapture>();
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IOperationCompletionListener, OperationCapture>(
                c => c.GetRequiredService<OperationCapture>()));
        });

    private static async Task<(Computed<int> Get, Computed<int> Count, Computed<int> CountOfLength)> Capture(
        InvalidationModeServiceBase service, string key)
    {
        var cGet = await Computed.Capture(() => service.Get(key));
        var cCount = await Computed.Capture(() => service.Count());
        var cCountOfLength = await Computed.Capture(() => service.CountOfLength(key.Length));
        return (cGet, cCount, cCountOfLength);
    }
}
