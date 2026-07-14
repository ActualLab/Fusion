using ActualLab.CommandR.Operations;
using ActualLab.Fusion.Operations.Internal;
using ActualLab.Fusion.Tests.OperationEvents;
using ActualLab.Testing.Logging;

namespace ActualLab.Fusion.Tests.Internal;

public class InvalidatingCommandCompletionHandlerTest(ITestOutputHelper @out) : SimpleFusionTestBase(@out)
{
    [Fact]
    public void DisqualifiedHandlerShapeIsWarnedOnce()
    {
        var loggerProvider = new CapturingLoggerProvider();
        using var services = CreateServices(s => {
            s.AddLogging(l => l.AddProvider(loggerProvider));
            s.AddSingleton<NonConventionalService>();
            s.AddCommander().AddHandlers<NonConventionalService>();
        });
        var handler = services.GetRequiredService<InvalidatingCommandCompletionHandler>();
        var command = new NonConventionalCommand();

        handler.IsRequired(command, out _, out _).Should().BeFalse();
        handler.IsRequired(command, out _, out _).Should().BeFalse();

        var content = loggerProvider.Content;
        content.Should().Contain("Invalidation replay is unsupported");
        (content.Split("Invalidation replay is unsupported").Length - 1).Should().Be(1);
    }

    [Fact]
    public async Task ScopedCommandServiceWorksWithValidateScopes()
    {
        // Regression: replay qualification must not resolve the handler's service -
        // under ValidateScopes, resolving a scoped service from the root provider throws
        var loggerProvider = new CapturingLoggerProvider();
        var capture = new CapturingOperationCompletionListener();
        var services = new ServiceCollection();
        services.AddSingleton<TestServiceProviderTag>();
        ConfigureServices(services);
        services.AddLogging(l => l.AddProvider(loggerProvider));
        services.AddScoped<ScopedCommandService>();
        services.AddCommander().AddHandlers<ScopedCommandService>();
        services.AddSingleton<IOperationCompletionListener>(capture);
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        StartServices(provider);

        var handler = provider.GetRequiredService<InvalidatingCommandCompletionHandler>();
        handler.IsRequired(new ScopedInterfaceCommand(), out _, out _).Should().BeFalse();
        handler.IsRequired(new ScopedMethodCommand(), out _, out _).Should().BeFalse();

        var commander = provider.Commander();
        await commander.Call(new ScopedInterfaceCommand());
        await commander.Call(new ScopedMethodCommand());

        // The harness below re-triggers the capturing listener, so the list is snapshotted here
        var operations = capture.Operations.ToList();
        operations.Should().HaveCount(2);
        foreach (var operation in operations)
            await OperationCompletionNoThrowTester.AssertCompletionListenersDoNotThrow(provider, operation);

        // A scoped non-compute service isn't replay material, so it must not be logged as disqualified
        loggerProvider.Content.Should().NotContain("Invalidation replay is unsupported");
    }

    // Nested types

    public record NonConventionalCommand : ICommand<Unit>;

    public class NonConventionalService : IComputeService, ICommandHandler<NonConventionalCommand>
    {
        public Task OnCommand(NonConventionalCommand command, CommandContext context, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    public record ScopedInterfaceCommand : ICommand<Unit>;

    public record ScopedMethodCommand : ICommand<Unit>;

    public class ScopedCommandService : ICommandHandler<ScopedInterfaceCommand>
    {
        public Task OnCommand(ScopedInterfaceCommand command, CommandContext context, CancellationToken cancellationToken)
        {
            InMemoryOperationScope.Require();
            return Task.CompletedTask;
        }

        [CommandHandler]
        public Task OnScopedMethodCommand(ScopedMethodCommand command, CancellationToken cancellationToken)
        {
            InMemoryOperationScope.Require();
            return Task.CompletedTask;
        }
    }

    private sealed class CapturingOperationCompletionListener : IOperationCompletionListener
    {
        public List<Operation> Operations { get; } = new();

        public Task OnOperationCompleted(Operation operation, CommandContext? commandContext)
        {
            lock (Operations)
                Operations.Add(operation);
            return Task.CompletedTask;
        }
    }
}
