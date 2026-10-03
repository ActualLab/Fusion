using ActualLab.CommandR.Operations;
using ActualLab.Fusion.Operations.Internal;
using ActualLab.Fusion.Tests.Services;

namespace ActualLab.Fusion.Tests.OperationEvents;

public class CompletionNoThrowTest(ITestOutputHelper @out) : SimpleFusionTestBase(@out)
{
    [Fact]
    public async Task RegisteredListenersDoNotThrow()
    {
        var capture = new CapturingOperationCompletionListener();
        var services = CreateServices(services => {
            services.AddFusion().AddService<IKeyValueService<string>, KeyValueService<string>>();
            services.AddSingleton<IOperationCompletionListener>(capture);
        });

        var commander = services.Commander();
        await commander.Call(new KeyValueService_Set<string>("k", "v"));

        capture.Operation.Should().NotBeNull("the Set command must go through TransientOperationScope");
        await OperationCompletionNoThrowTester.AssertCompletionListenersDoNotThrow(
            services, capture.Operation!, capture.CommandContext);
    }

    [Fact]
    public async Task AThrowingDeferredBlockDoesNotFailTheCommand()
    {
        var services = CreateServices(services =>
            services.AddFusion().AddService<IThrowingInvalidationService, ThrowingInvalidationService>());

        var service = services.GetRequiredService<IThrowingInvalidationService>();
        var computed = await Computed.Capture(() => service.Get("k"));

        // The mutation already committed when the block runs, so failing the command is not an option
        await services.Commander().Call(new ThrowingInvalidation_Touch("k"));

        // ... and the block threw before reaching its invalidation call, so the value stays consistent
        computed.IsConsistent().Should().BeTrue();
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

[DeferredInvalidationMode(DeferredInvalidationMode.Local)]
public class ThrowingInvalidationService : IThrowingInvalidationService
{
    public virtual Task<string> Get(string key, CancellationToken cancellationToken = default)
        => Task.FromResult(key);

    public virtual Task OnTouch(ThrowingInvalidation_Touch command, CancellationToken cancellationToken = default)
    {
        TransientOperationScope.Require();
        Invalidation.Defer(() => throw new InvalidOperationException("Invalidation block failed."));
        return Task.CompletedTask;
    }
}
