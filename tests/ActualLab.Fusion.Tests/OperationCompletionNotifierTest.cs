using ActualLab.CommandR.Operations;
using ActualLab.Tests;

namespace ActualLab.Fusion.Tests;

public class OperationCompletionNotifierTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public async Task ExternalCompletionFailureUnmarksAndPropagates()
    {
        var listener = new FailingListener();
        var services = BuildServices(listener);
        var notifier = new OperationCompletionNotifier(
            new OperationCompletionNotifier.Options { Clock = SystemClock.Instance },
            services);
        var operation = new Operation("ext-uuid-1", "another-host") {
            LoggedAt = SystemClock.Instance.Now,
            Command = LocalCommand.New(() => Task.CompletedTask),
        };

        // External op (null CommandContext): the completion failure must surface to the caller
        await Assert.ThrowsAnyAsync<Exception>(
            () => notifier.NotifyCompleted(operation, null));
        listener.CallCount.Should().Be(1);

        // ...and the UUID must be unmarked, so a redelivery actually re-dispatches instead of being deduped
        await Assert.ThrowsAnyAsync<Exception>(
            () => notifier.NotifyCompleted(operation, null));
        listener.CallCount.Should().Be(2);
    }

    [Fact]
    public async Task LocalCompletionFailureIsSwallowedAndKeepsUuidMarked()
    {
        var listener = new FailingListener();
        var services = BuildServices(listener);
        var notifier = new OperationCompletionNotifier(
            new OperationCompletionNotifier.Options { Clock = SystemClock.Instance },
            services);
        var hostId = services.GetRequiredService<HostId>();
        var commander = services.GetRequiredService<ICommander>();
        var command = LocalCommand.New(() => Task.CompletedTask);
        var context = CommandContext.New(commander, command, isOutermost: true);
        var operation = new Operation("loc-uuid-1", hostId.Id) {
            LoggedAt = SystemClock.Instance.Now,
            Command = command,
        };

        // Local op (non-null CommandContext): the failure is logged, not propagated
        var result = await notifier.NotifyCompleted(operation, context);
        result.Should().BeTrue();
        listener.CallCount.Should().Be(1);

        // The UUID stays marked, so a duplicate notification is deduplicated
        var result2 = await notifier.NotifyCompleted(operation, context);
        result2.Should().BeFalse();
        listener.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task SynchronousListenerThrowUnmarksAndPropagatesForExternalOperation()
    {
        var listener = new SyncThrowingListener();
        var services = BuildServices(listener);
        var notifier = new OperationCompletionNotifier(
            new OperationCompletionNotifier.Options { Clock = SystemClock.Instance },
            services);
        var operation = new Operation("ext-sync-uuid-1", "another-host") {
            LoggedAt = SystemClock.Instance.Now,
            Command = LocalCommand.New(() => Task.CompletedTask),
        };

        // A synchronous throw on an external op must flow through the external-terminal path
        await Assert.ThrowsAnyAsync<Exception>(
            () => notifier.NotifyCompleted(operation, null));
        listener.CallCount.Should().Be(1);

        // ...and unmark the UUID, so a redelivery actually re-dispatches instead of being deduped
        await Assert.ThrowsAnyAsync<Exception>(
            () => notifier.NotifyCompleted(operation, null));
        listener.CallCount.Should().Be(2);
    }

    [Fact]
    public async Task ExternalCompletionCommandFailurePropagatesThroughCompletionProducer()
    {
        var services = BuildFusionServices();
        var hostId = services.GetRequiredService<HostId>();
        var notifier = services.GetRequiredService<IOperationCompletionNotifier>();
        var command = LocalCommand.New(() => Task.CompletedTask);

        // External op (null CommandContext, foreign HostId): CompletionProducer must rethrow,
        // so the failure surfaces out of NotifyCompleted
        var externalOperation = new Operation("ext-prod-uuid-1", "another-host") {
            LoggedAt = SystemClock.Instance.Now,
            Command = command,
        };
        await Assert.ThrowsAnyAsync<Exception>(
            () => notifier.NotifyCompleted(externalOperation, null));

        // Local op (non-null CommandContext, local HostId): the same failure is swallowed
        var commander = services.GetRequiredService<ICommander>();
        var context = CommandContext.New(commander, command, isOutermost: true);
        var localOperation = new Operation("loc-prod-uuid-1", hostId.Id) {
            LoggedAt = SystemClock.Instance.Now,
            Command = command,
        };
        var result = await notifier.NotifyCompleted(localOperation, context);
        result.Should().BeTrue();
    }

    private static IServiceProvider BuildServices(IOperationCompletionListener listener)
    {
        var services = new ServiceCollection();
        services.AddCommander();
        services.AddSingleton(_ => new HostId("local-test-host"));
        services.AddSingleton(listener);
        return services.BuildServiceProvider();
    }

    private static IServiceProvider BuildFusionServices()
    {
        var services = new ServiceCollection();
        var commander = services.AddCommander();
        services.AddFusion();
        services.AddSingleton<FailingCompletionHandler>();
        commander.AddHandlers<FailingCompletionHandler>();
        return services.BuildServiceProvider();
    }

    // Nested types

    private sealed class FailingListener : IOperationCompletionListener
    {
        private int _callCount;
        public int CallCount => Volatile.Read(ref _callCount);

        public Task OnOperationCompleted(Operation operation, CommandContext? commandContext)
        {
            Interlocked.Increment(ref _callCount);
            return Task.FromException(new InvalidOperationException("Completion failed (test)"));
        }
    }

    private sealed class SyncThrowingListener : IOperationCompletionListener
    {
        private int _callCount;
        public int CallCount => Volatile.Read(ref _callCount);

        public Task OnOperationCompleted(Operation operation, CommandContext? commandContext)
        {
            Interlocked.Increment(ref _callCount);
            throw new InvalidOperationException("Completion failed synchronously (test)");
        }
    }

    private sealed class FailingCompletionHandler : ICommandHandler<ICompletion>
    {
        [CommandFilter(Priority = 1_000_000)]
        public Task OnCommand(ICompletion command, CommandContext context, CancellationToken cancellationToken)
            => Task.FromException(new InvalidOperationException("Completion command failed (test)"));
    }
}
