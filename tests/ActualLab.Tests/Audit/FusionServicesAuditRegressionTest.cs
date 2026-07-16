using System.Reflection;
using ActualLab.Diagnostics;
using ActualLab.Fusion.Blazor;
using ActualLab.Fusion.Diagnostics;
using ActualLab.Fusion.Extensions;
using ActualLab.Fusion.Operations.Internal;

namespace ActualLab.Tests.Audit;

public class FusionServicesAuditRegressionTest
{
    [Fact]
    public void ByValueComparerInstanceShouldCompareByValue()
    {
        var first = new string('x', 8);
        var second = new string('x', 8);

        ByValueParameterComparer.Instance.Should().BeOfType<ByValueParameterComparer>();
        ByValueParameterComparer.Instance.AreEqual(first, second).Should().BeTrue();
    }

    [Fact]
    public void WithTagsShouldReplaceExistingTags()
    {
        var session = new Session("session-id&a=1");

        session.WithTags("b=2").Id.Should().Be("session-id&b=2");
        session.WithTags("").Id.Should().Be("session-id");
    }

    [Fact]
    public void WithTagShouldReplaceATagWithoutDroppingFollowingTags()
    {
        var session = new Session("session-id&a=1&b=2");

        session.WithTag("a", "3").Id.Should().Be("session-id&a=3&b=2");
    }

    [Fact]
    public async Task InMemoryOperationShouldAwaitCompletionHandlers()
    {
        var state = new CompletionOrderingState();
        var services = new ServiceCollection();
        services.AddFusion();
        services.AddSingleton(state);
        services.AddSingleton<CompletionOrderingHandler>();
        services.AddCommander().AddHandlers<CompletionOrderingHandler>();
        await using var serviceProvider = services.BuildServiceProvider();

        var callTask = serviceProvider.Commander().Call(new CompletionOrderingCommand());
        await state.WhenStarted.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        var completedBeforeHandler = false;
        try {
            completedBeforeHandler = ReferenceEquals(
                await Task.WhenAny(callTask, Task.Delay(TimeSpan.FromMilliseconds(250))).ConfigureAwait(false),
                callTask);
        }
        finally {
            state.AllowCompletion.TrySetResult();
        }
        await callTask.ConfigureAwait(false);
        completedBeforeHandler.Should().BeFalse();
    }

    [Fact]
    public async Task JustDisconnectedStateShouldRemainValidForItsConfiguredPeriod()
    {
        var services = new ServiceCollection();
        services.AddFusion();
        await using var serviceProvider = services.BuildServiceProvider();
        await using var monitor = new TestRpcPeerStateMonitor(serviceProvider) {
            JustConnectedPeriod = TimeSpan.FromMilliseconds(20),
            JustDisconnectedPeriod = TimeSpan.FromMilliseconds(200),
            ExtraInvalidationDelay = TimeSpan.Zero,
        };
        monitor.Disconnect(TimeSpan.FromMilliseconds(50));

        var state = await monitor.State.Use().ConfigureAwait(false);

        state.Kind.Should().Be(RpcPeerStateKind.JustDisconnected);
        monitor.State.Computed.ConsistencyState.Should().Be(ConsistencyState.Consistent);
    }

    [Fact]
    public async Task FusionMonitorShouldCountTheFirstUnregistration()
    {
        var services = new ServiceCollection();
        services.AddFusion();
        await using var serviceProvider = services.BuildServiceProvider();
        await using var monitor = new FusionMonitor(serviceProvider) {
            RegistrationSampler = Sampler.Always,
        };
        var source = new ComputedSource<int>(serviceProvider, 0, (_, _) => Task.FromResult(1), "audit");
        var onUnregistration = typeof(FusionMonitor).GetMethod(
            "OnUnregistration",
            BindingFlags.Instance | BindingFlags.NonPublic)!;

        onUnregistration.Invoke(monitor, [source.Computed]);

        var statistics = typeof(FusionMonitor)
            .GetField("_statistics", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(monitor)!;
        var registrations = (Dictionary<string, (int, int)>)statistics.GetType()
            .GetProperty("Registrations")!
            .GetValue(statistics)!;
        registrations["audit"].Should().Be((0, 1));
    }

    private sealed record CompletionOrderingCommand : ICommand<Unit>;

    private sealed class CompletionOrderingState
    {
        public TaskCompletionSource WhenStarted { get; } = TaskCompletionSourceExt.New();
        public TaskCompletionSource AllowCompletion { get; } = TaskCompletionSourceExt.New();
    }

    private sealed class CompletionOrderingHandler(CompletionOrderingState state)
    {
        [CommandHandler]
        public Task OnCompletionOrderingCommand(
            CompletionOrderingCommand command,
            CancellationToken cancellationToken)
        {
            var scope = InMemoryOperationScope.GetOrCreate(CommandContext.GetCurrent());
            scope.Operation.AddCompletionHandler(async _ => {
                state.WhenStarted.TrySetResult();
                await state.AllowCompletion.Task.ConfigureAwait(false);
            });
            return Task.CompletedTask;
        }
    }

    private sealed class TestRpcPeerStateMonitor(IServiceProvider services)
        : RpcPeerStateMonitor(services, null, mustStart: false)
    {
        public void Disconnect(TimeSpan ago)
        {
            var disconnectedAt = RpcHub.SystemClock.Now - ago;
            ((IMutableState<RpcPeerRawState>)RawState).Value =
                new RpcPeerRawDisconnectedState(disconnectedAt, default, null);
            State = Services.StateFactory().NewComputed(ComputeState, GetType().Name);
        }
    }
}
