using ActualLab.Fusion.Operations.Internal;
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

    public record NonConventionalCommand : ICommand<Unit>;

    public class NonConventionalService : IComputeService, ICommandHandler<NonConventionalCommand>
    {
        public Task OnCommand(NonConventionalCommand command, CommandContext context, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
