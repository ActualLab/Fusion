using ActualLab.CommandR.Operations;
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
