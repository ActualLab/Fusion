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

    private static IServiceProvider BuildServices(IOperationCompletionListener listener)
    {
        var services = new ServiceCollection();
        services.AddCommander();
        services.AddSingleton(_ => new HostId("local-test-host"));
        services.AddSingleton(listener);
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
}
