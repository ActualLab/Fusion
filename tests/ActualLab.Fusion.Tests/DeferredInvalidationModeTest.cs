using System.Reflection;
using ActualLab.Interception;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ActualLab.CommandR.Operations;
using ActualLab.Fusion.Operations;
using ActualLab.Fusion.Operations.Internal;
using ActualLab.Fusion.Tests.Services;
using ActualLab.Reflection;
using ActualLab.Serialization;

namespace ActualLab.Fusion.Tests;

public class DeferredInvalidationModeTest(ITestOutputHelper @out) : SimpleFusionTestBase(@out)
{
    [Fact]
    public async Task Local_InvalidatesInProcessWithoutReplay()
    {
        var services = CreateHostServices<LocalDeferredDeferredInvalidationModeService>();
        var kv = services.GetRequiredService<LocalDeferredDeferredInvalidationModeService>();

        var (cGet, cCount, cLength) = await Capture(kv, "a");
        await services.Commander().Call(new DeferredInvalidationModeService_Set("a", 1));

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
        var services = CreateHostServices<LocalDeferredDeferredInvalidationModeService>();
        await services.Commander().Call(new DeferredInvalidationModeService_Set("a", 1));

        var operation = services.GetRequiredService<OperationCapture>().Operations.Single();
        operation.InvalidationCalls.Should().BeEmpty();
    }

    // Replicated is only as reliable as its carrier, so it's rejected unless the operation is stored -
    // see InvalidationModeDbTest for the path that works. Both cases below fail per call, not at
    // startup, because the mode is only known once a handler actually defers something.

    [Fact]
    public async Task Replicated_WithATransientOperationThrows()
    {
        var services = CreateHostServices<TransientReplicatedDeferredDeferredInvalidationModeService>();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => services.Commander().Call(new DeferredInvalidationModeService_Set("a", 1)));
    }

    [Fact]
    public async Task Replicated_WithoutAnOperationThrows()
    {
        var services = CreateHostServices<ScopelessReplicatedDeferredDeferredInvalidationModeService>();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => services.Commander().Call(new DeferredInvalidationModeService_Set("a", 1)));
    }

    [Fact]
    public async Task Distributed_WithATransientOperationThrows()
    {
        // Distributed needs a stored event to recover from, for the same reason Replicated
        // needs a stored operation: the carrier has to commit with the mutation
        var services = CreateHostServices<TransientDistributedDeferredInvalidationModeService>();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => services.Commander().Call(new DeferredInvalidationModeService_Set("a", 1)));
    }

    [Fact]
    public async Task Distributed_WithoutAnOperationThrows()
    {
        var services = CreateHostServices<ScopelessDistributedDeferredInvalidationModeService>();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => services.Commander().Call(new DeferredInvalidationModeService_Set("a", 1)));
    }

    [Fact]
    public async Task Defer_RecordsCallsToADistributedService()
    {
        // A Distributed-mode service is served by RemoteComputeMethodFunction even locally, and
        // CaptureInvalidation implies GetExisting - so without its own recording branch there, the
        // call short-circuits in TryUseExisting and is silently not recorded
        var services = CreateServices(s => s.AddFusion()
            .AddDistributedService<IDistributedCounter, DistributedCounter>());
        var counter = services.GetRequiredService<IDistributedCounter>();

        var context = new DeferredInvalidationContext {
            Mode = DeferredInvalidationMode.Replicated,
        };
        using (context.Activate())
            Invalidation.Defer(() => _ = counter.Get("a", default));

        var calls = await context.CollectInvalidationCalls();
        calls.Should().HaveCount(1);
        // The RPC-style name carries the parameter count, CancellationToken included
        calls[0].MethodName.Should().Be("Get:2");
    }

    [Fact]
    public async Task RecordedCalls_ApplyThroughTheServiceProxy()
    {
        var services = CreateHostServices<LocalDeferredDeferredInvalidationModeService>();
        var kv = services.GetRequiredService<LocalDeferredDeferredInvalidationModeService>();
        var type = typeof(LocalDeferredDeferredInvalidationModeService);
        var calls = ImmutableList.Create(
            NewInvocation(type, "Get", ArgumentList.New("ab", default(CancellationToken))),
            NewInvocation(type, "Count", ArgumentList.New(default(CancellationToken))),
            NewInvocation(type, "CountOfLength", ArgumentList.New(2, default(CancellationToken))));

        var (cGet, cCount, cLength) = await Capture(kv, "ab");
        var cOther = await Computed.Capture(() => kv.Get("other"));

        await services.GetRequiredService<FusionOperationCompletionHandler>()
            .ApplyLocalInvalidations(calls);

        cGet.IsConsistent().Should().BeFalse();
        cCount.IsConsistent().Should().BeFalse();
        cLength.IsConsistent().Should().BeFalse();
        cOther.IsConsistent().Should().BeTrue();
    }

    [Fact]
    public async Task RecordedCalls_SurviveTheOperationLogSerializer()
    {
        var services = CreateHostServices<LocalDeferredDeferredInvalidationModeService>();
        var kv = services.GetRequiredService<LocalDeferredDeferredInvalidationModeService>();
        var serviceType = new TypeRef(typeof(LocalDeferredDeferredInvalidationModeService)).WithoutAssemblyVersions();
        var type = typeof(LocalDeferredDeferredInvalidationModeService);
        var calls = ImmutableList.Create(
            NewInvocation(type, "Get", ArgumentList.New("ab", default(CancellationToken))),
            NewInvocation(type, "CountOfLength", ArgumentList.New(2, default(CancellationToken))));

        var json = NewtonsoftJsonSerializer.Default.Write(calls);
        var restored = NewtonsoftJsonSerializer.Default.Read<ImmutableList<ServiceCall>>(json);

        restored.Select(x => x.MethodName).Should().Equal("Get:2", "CountOfLength:2");
        restored[0].Arguments.Get<string>(0).Should().Be("ab");
        var (cGet, _, cLength) = await Capture(kv, "ab");
        await services.GetRequiredService<FusionOperationCompletionHandler>()
            .ApplyLocalInvalidations(restored);

        cGet.IsConsistent().Should().BeFalse();
        cLength.IsConsistent().Should().BeFalse();
    }

    [Fact]
    public async Task AHandlerThatDefersNothingInvalidatesNothing()
    {
        var services = CreateHostServices<DelegatingDeferredInvalidationModeService>();
        var kv = services.GetRequiredService<DelegatingDeferredInvalidationModeService>();

        var (cGet, cCount, cLength) = await Capture(kv, "a");
        await services.Commander().Call(new DeferredInvalidationModeService_Set("a", 1));

        cGet.IsConsistent().Should().BeTrue();
        cCount.IsConsistent().Should().BeTrue();
        cLength.IsConsistent().Should().BeTrue();
        kv.MutationCount.Should().Be(1);
    }

    [Fact]
    public async Task TheNestedHandlerThatDefersDecidesTheMode()
    {
        var services = CreateHostServices<DelegatingDeferredInvalidationModeService>();
        var kv = services.GetRequiredService<DelegatingDeferredInvalidationModeService>();

        var (cGet, cCount, cLength) = await Capture(kv, "a");
        await services.Commander().Call(new DeferredInvalidationModeService_SetViaNested("a", 1));

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
        using var _1 = new DeferredInvalidationContext { Mode = DeferredInvalidationMode.Local }.Activate();
        using var _2 = Invalidation.Begin();
        Assert.Throws<InvalidOperationException>(() => Invalidation.Defer(() => { }));
    }

    [Fact]
    public async Task NestedCommand_InsideInvalidationPass_Throws()
    {
        // Nothing replays a command handler anymore, so a command inside an invalidation pass is a
        // bug - and a silent one if it ran, since every scope provider opts out while invalidating.
        // Only a nested command can get here: Commander suppresses the ExecutionContext flow of an
        // outermost one, so that one never sees its caller's ComputeContext.
        var services = CreateHostServices<CommandDuringInvalidationService>();

        var error = await Assert.ThrowsAnyAsync<InvalidOperationException>(
            () => services.Commander().Call(new DeferredInvalidationModeService_Set("a", 1)));
        error.Message.Should().Contain(nameof(DeferredInvalidationModeService_SetLocal));
    }

    [Fact]
    public async Task OutermostCommand_InsideInvalidationPass_Runs()
    {
        // The counterpart of the above: Commander.Run suppresses the ExecutionContext flow for an
        // outermost command, so the invalidation pass around this call doesn't reach its pipeline -
        // and the command runs with its Operations Framework intact rather than silently without it
        var services = CreateHostServices<LocalDeferredDeferredInvalidationModeService>();
        var kv = services.GetRequiredService<LocalDeferredDeferredInvalidationModeService>();

        using (Invalidation.Begin())
            await services.Commander().Call(new DeferredInvalidationModeService_Set("a", 1));

        (await kv.Get("a")).Should().Be(1);
        kv.MutationCount.Should().Be(1);
    }

    [Fact]
    public void BeginDeferred_InsideInvalidationPass_Throws()
    {
        using var _ = Invalidation.Begin();
        Assert.Throws<InvalidOperationException>(() => new DeferredInvalidationContext { Mode = DeferredInvalidationMode.Local }.Activate());
    }

    [Fact]
    public void Activate_InsideAnotherContext_Throws()
    {
        using var _1 = new DeferredInvalidationContext { Mode = DeferredInvalidationMode.Local }.Activate();

        // One operation gets one carrier, so a nested context has to be asked for explicitly
        Assert.Throws<InvalidOperationException>(
            () => new DeferredInvalidationContext { Mode = DeferredInvalidationMode.Local }.Activate());
    }

    [Fact]
    public void Activate_NestsWhenAsked()
    {
        var outer = new DeferredInvalidationContext { Mode = DeferredInvalidationMode.Local };
        using var _1 = outer.Activate();
        var inner = new DeferredInvalidationContext {
            Mode = DeferredInvalidationMode.Local,
        };

        using (inner.Activate(requireOutermost: false)) {
            Invalidation.Defer(() => { });
            // A nested context takes the blocks added while it's the current one
            inner.BlockCount.Should().Be(1);
            outer.BlockCount.Should().Be(0);
        }

        DeferredInvalidationContext.Current.Should().BeSameAs(outer);
    }

    [Fact]
    public async Task TwoNestedModes_Throw()
    {
        // One operation gets one carrier, so a second nested handler needing a different mode
        // can't be honoured - and silently narrowing it is what the check exists to prevent
        var services = CreateHostServices<MixedModesService>();

        var error = await Assert.ThrowsAnyAsync<InvalidOperationException>(
            () => services.Commander().Call(new MixedModes_Set("a")));
        // Both modes, so the message says which two couldn't be reconciled
        error.Message.Should().Contain(nameof(DeferredInvalidationMode.Local));
        error.Message.Should().Contain(nameof(DeferredInvalidationMode.Replicated));
    }

    [Fact]
    public void Mode_IsUndecidedUntilAHandlerDefers()
    {
        var context = new DeferredInvalidationContext {
            ModeResolver = NewModeResolver(DeferredInvalidationMode.Replicated),
        };

        context.Mode.Should().BeNull();

        using (context.Activate())
            Invalidation.Defer(() => { });

        // The resolver is asked when the block is added, not when the context is created
        context.Mode.Should().Be(DeferredInvalidationMode.Replicated);
    }

    [Fact]
    public void ABlockCannotBeAddedOnceClosed()
    {
        var context = new DeferredInvalidationContext {
            Mode = DeferredInvalidationMode.Local,
        };

        using (context.Activate()) {
            context.IsClosed.Should().BeFalse();
            Invalidation.Defer(() => { });
        }

        context.IsClosed.Should().BeTrue();
        // Silently dropping it is what hid a block deferred from a task the handler left behind
        Assert.Throws<InvalidOperationException>(() => context.AddBlock(() => { }));
    }

    [Fact]
    public void DeactivatingClosesTheContext()
    {
        var context = new DeferredInvalidationContext {
            Mode = DeferredInvalidationMode.Local,
        };

        using (context.Activate())
            context.IsClosed.Should().BeFalse();

        context.IsClosed.Should().BeTrue();
    }

    [Fact]
    public async Task ReadingBlocksTwiceYieldsTheSameCalls()
    {
        var counter = CreateHostServices<LocalDeferredDeferredInvalidationModeService>()
            .GetRequiredService<LocalDeferredDeferredInvalidationModeService>();
        var context = new DeferredInvalidationContext {
            Mode = DeferredInvalidationMode.Replicated,
        };

        using (context.Activate())
            Invalidation.Defer(() => _ = counter.Get("a", default));

        // Nothing is consumed by a read - safe precisely because a closed context can't gain more
        var first = await context.CollectInvalidationCalls();
        var second = await context.CollectInvalidationCalls();

        first.Should().HaveCount(1);
        second.Select(x => x.MethodName).Should().Equal(first.Select(x => x.MethodName));
    }

    [Fact]
    public void BlocksCannotBeReadBeforeClose()
    {
        var context = new DeferredInvalidationContext { Mode = DeferredInvalidationMode.Local };

        using (context.Activate()) {
            Invalidation.Defer(() => { });
            // More blocks can still arrive, so a read here would be a partial answer
            Assert.Throws<InvalidOperationException>(() => context.GetBlocks());
        }

        context.GetBlocks().Should().HaveCount(1);
    }

    [Fact]
    public void AContextCannotBeActivatedTwice()
    {
        var context = new DeferredInvalidationContext {
            Mode = DeferredInvalidationMode.Local,
        };

        using (context.Activate()) { }

        Assert.Throws<InvalidOperationException>(() => context.Activate());
    }

    [Fact]
    public void Mode_WithoutAResolverOrAModeThrows()
    {
        var context = new DeferredInvalidationContext();

        using var _ = context.Activate();
        Assert.Throws<InvalidOperationException>(() => Invalidation.Defer(() => { }));
    }

    [Fact]
    public async Task Apply_RunsBlocksAfterTheScopeCloses()
    {
        var services = CreateHostServices<LocalDeferredDeferredInvalidationModeService>();
        var kv = services.GetRequiredService<LocalDeferredDeferredInvalidationModeService>();
        var cGet = await Computed.Capture(() => kv.Get("a"));

        var context = new DeferredInvalidationContext { Mode = DeferredInvalidationMode.Local };
        using (context.Activate()) {
            Invalidation.Defer(() => _ = kv.Get("a", default));
            cGet.IsConsistent().Should().BeTrue();
        }
        // Closing the scope only makes the blocks final - running them is the caller's call
        cGet.IsConsistent().Should().BeTrue();

        await context.InvokeBlocks(new InvalidationSource(nameof(Apply_RunsBlocksAfterTheScopeCloses)));
        cGet.IsConsistent().Should().BeFalse();
    }

    [Fact]
    public async Task CustomResolver_MovesLocalServiceToReplicated()
    {
        // The service runs on a transient operation, so the resolver taking effect is exactly what
        // makes it fail - see InvalidationModeDbTest for the same move on a stored operation
        var services = CreateHostServices<LocalDeferredDeferredInvalidationModeService>(
            // The resolver is an ordinary singleton - registering one after AddFusion() wins
            configureFusion: f => f.Services.AddSingleton<DeferredInvalidationModeResolver>(
                c => new ReplicatedModeResolver(
                    c.GetRequiredService<ServiceTypeResolver>(),
                    typeof(LocalDeferredDeferredInvalidationModeService))));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => services.Commander().Call(new DeferredInvalidationModeService_Set("a", 1)));
    }

    [Fact]
    public void DefaultResolver_ReadsTheServiceTypeAttribute()
    {
        var services = CreateHostServices<LocalDeferredDeferredInvalidationModeService>();
        var resolver = services.GetRequiredService<DeferredInvalidationModeResolver>();

        // The handler method carries no attribute of its own - its service type's one applies
        resolver.Resolve(services, new DeferredInvalidationModeService_Set("a", 1))
            .Should().Be(DeferredInvalidationMode.Local);
    }

    [Fact]
    public void DefaultResolver_WithoutAnAttributeThrows()
    {
        // There is no default mode: how far an invalidation has to reach is something only the
        // handler knows, so a handler that defers without declaring it is a bug, not a Local one
        var services = CreateHostServices<UndeclaredDeferredInvalidationModeService>();
        var resolver = services.GetRequiredService<DeferredInvalidationModeResolver>();

        var error = Assert.Throws<InvalidOperationException>(
            () => resolver.Resolve(services, new DeferredInvalidationModeService_Set("a", 1)));
        error.Message.Should().Contain(nameof(DeferredInvalidationModeAttribute));
    }

    [Fact]
    public async Task Defer_WithoutADeclaredModeThrows()
    {
        var services = CreateHostServices<UndeclaredDeferredInvalidationModeService>();

        var error = await Assert.ThrowsAnyAsync<InvalidOperationException>(
            () => services.Commander().Call(new DeferredInvalidationModeService_Set("a", 1)));
        error.Message.Should().Contain(nameof(DeferredInvalidationModeAttribute));
    }

    [Fact]
    public void DefaultResolver_ReadsTheImplementationAttributeBehindAnInterface()
    {
        // The handler is declared on the interface, so its Method.DeclaringType carries no
        // attribute - only the implementation the container maps that interface to does
        var services = CreateServices(services => {
            var fusion = services.AddFusion();
            fusion.AddService<IInterfaceDeclaredService, InterfaceDeclaredService>();
        });
        var resolver = services.GetRequiredService<DeferredInvalidationModeResolver>();

        resolver.Resolve(services, new InterfaceDeclaredService_Set("a"))
            .Should().Be(DeferredInvalidationMode.Replicated);
    }

    [Fact]
    public void DefaultResolver_ReadsTheImplementationAttributeBehindAnAliasedInterface()
    {
        // Regression: a second interface pointing at the same implementation - IAuthBackend next
        // to IAuth - registers its handlers under itself, so the mode was resolved from that
        // interface alone and silently fell back to the default
        // Exactly how AddAuthService registers IAuth + IAuthBackend
        var services = CreateServices(services => {
            var fusion = services.AddFusion();
            fusion.AddService(typeof(IInterfaceDeclaredService), typeof(InterfaceDeclaredService),
                hasCommandHandlers: false);
            fusion.ServiceTypeResolver.RegisterAlias(
                typeof(IInterfaceDeclaredBackend), typeof(InterfaceDeclaredService));
            services.AddSingleton(c => (IInterfaceDeclaredBackend)c.GetRequiredService<IInterfaceDeclaredService>());
            fusion.Commander.AddHandlers(typeof(IInterfaceDeclaredService));
            fusion.Commander.AddHandlers(typeof(IInterfaceDeclaredBackend));
        });
        var resolver = services.GetRequiredService<DeferredInvalidationModeResolver>();

        resolver.Resolve(services, new InterfaceDeclaredBackend_Set("a"))
            .Should().Be(DeferredInvalidationMode.Replicated);
        // The alias must not steal the implementation's primary service type
        services.GetRequiredService<ServiceTypeResolver>()
            .TryResolveServiceType(typeof(InterfaceDeclaredService))
            .Should().Be(typeof(IInterfaceDeclaredService));
    }

    // Nested types

    private static DeferredInvalidationModeResolver NewModeResolver(DeferredInvalidationMode mode)
        => new FixedModeResolver(new ServiceTypeResolver(), mode);

    private sealed class FixedModeResolver(
        ServiceTypeResolver serviceTypeResolver,
        DeferredInvalidationMode mode
        ) : DeferredInvalidationModeResolver(serviceTypeResolver)
    {
        public override DeferredInvalidationMode Resolve()
            => mode;
    }

    private sealed class ReplicatedModeResolver(
        ServiceTypeResolver serviceTypeResolver,
        Type targetType
        ) : DeferredInvalidationModeResolver(serviceTypeResolver)
    {
        public override DeferredInvalidationMode Resolve(IMethodCommandHandler handler)
            => handler.GetHandlerServiceType() == targetType
                ? DeferredInvalidationMode.Replicated
                : base.Resolve(handler);
    }

    // Private methods

    private IServiceProvider CreateHostServices<TService>(
        Action<FusionBuilder>? configureFusion = null,
        Action<IServiceCollection>? configureServices = null)
        where TService : class, IComputeService
        => CreateServices(services => {
            configureServices?.Invoke(services);
            var fusion = services.AddFusion();
            fusion.AddService<TService>();
            configureFusion?.Invoke(fusion);
            services.AddSingleton<OperationCapture>();
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IOperationCompletionListener, OperationCapture>(
                c => c.GetRequiredService<OperationCapture>()));
        });

    private static ServiceCall NewInvocation(Type type, string name, ArgumentList arguments)
        => ServiceCall.New(
            type, MethodInfoExt.GetByRpcStyleName(type, $"{name}:{arguments.Length}"), arguments);

    private static async Task<(Computed<int> Get, Computed<int> Count, Computed<int> CountOfLength)> Capture(
        DeferredInvalidationModeServiceBase service, string key)
    {
        var cGet = await Computed.Capture(() => service.Get(key));
        var cCount = await Computed.Capture(() => service.Count());
        var cCountOfLength = await Computed.Capture(() => service.CountOfLength(key.Length));
        return (cGet, cCount, cCountOfLength);
    }
}
