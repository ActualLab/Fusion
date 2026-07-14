using ActualLab.CommandR.Operations;
using ActualLab.Fusion.Operations.Internal;
using ActualLab.Fusion.Tests.Services;

namespace ActualLab.Fusion.Tests.OperationEvents;

public class CompletionNoThrowTest(ITestOutputHelper @out) : SimpleFusionTestBase(@out)
{
    [Fact]
    public async Task RegisteredListenersAndInvalidationPassDoNotThrow()
    {
        var capture = new CapturingOperationCompletionListener();
        var services = CreateServices(services => {
            services.AddFusion().AddService<IKeyValueService<string>, KeyValueService<string>>();
            services.AddSingleton<IOperationCompletionListener>(capture);
        });

        var commander = services.Commander();
        await commander.Call(new KeyValueService_Set<string>("k", "v"));

        capture.Operation.Should().NotBeNull("the Set command must go through InMemoryOperationScope");
        var operation = capture.Operation!;

        await OperationCompletionNoThrowTester.AssertCompletionListenersDoNotThrow(
            services, operation, capture.CommandContext);
        await OperationCompletionNoThrowTester.AssertInvalidationPassDoesNotThrow(commander, operation);
    }

    [Fact]
    public async Task InvalidationPassViolationIsDetected()
    {
        var capture = new CapturingOperationCompletionListener();
        var services = CreateServices(services => {
            services.AddFusion().AddService<IThrowingInvalidationService, ThrowingInvalidationService>();
            services.AddSingleton<IOperationCompletionListener>(capture);
        });

        var commander = services.Commander();
        // The command itself succeeds: the real handler's replay pass swallows the invalidation failure.
        await commander.Call(new ThrowingInvalidation_Touch("k"));

        capture.Operation.Should().NotBeNull("the command must go through InMemoryOperationScope");
        var operation = capture.Operation!;

        Func<Task> act = () => OperationCompletionNoThrowTester.AssertInvalidationPassDoesNotThrow(commander, operation);
        await act.Should().ThrowAsync<Exception>(
            "the harness must surface an invalidation pass that throws in its Invalidation.IsActive branch");
    }

    // Nested types

    private sealed class CapturingOperationCompletionListener : IOperationCompletionListener
    {
        public Operation? Operation { get; private set; }
        public CommandContext? CommandContext { get; private set; }

        public Task OnOperationCompleted(Operation operation, CommandContext? commandContext)
        {
            Operation = operation;
            CommandContext = commandContext;
            return Task.CompletedTask;
        }
    }
}

public interface IThrowingInvalidationService : IComputeService
{
    [ComputeMethod]
    Task<string> Get(string key, CancellationToken cancellationToken = default);
    [CommandHandler]
    Task OnTouch(ThrowingInvalidation_Touch command, CancellationToken cancellationToken = default);
}

public record ThrowingInvalidation_Touch(string Key) : ICommand<Unit>;

public class ThrowingInvalidationService : IThrowingInvalidationService
{
    public virtual Task<string> Get(string key, CancellationToken cancellationToken = default)
        => Task.FromResult(key);

    public virtual Task OnTouch(ThrowingInvalidation_Touch command, CancellationToken cancellationToken = default)
    {
        if (Invalidation.IsActive)
            throw new InvalidOperationException("Invalidation branch failed.");

        InMemoryOperationScope.Require();
        return Task.CompletedTask;
    }
}
