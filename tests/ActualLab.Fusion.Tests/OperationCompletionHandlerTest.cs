using ActualLab.CommandR.Operations;
using ActualLab.Fusion.Operations.Internal;

namespace ActualLab.Fusion.Tests;

public class OperationCompletionHandlerTest(ITestOutputHelper @out) : SimpleFusionTestBase(@out)
{
    [Fact]
    public void OperationCompletion_HasExactlyOneHandler()
    {
        // FusionOperationCompletionHandler both implements ICommandHandler<OperationCompletion> and
        // inherits an OnCommand carrying [CommandHandler], so AddHandlers has two chances to
        // register it - once "via interface", once as a method - and two entries would run every
        // operation's completion twice. CommandR dedupes them through the interface map; this pins
        // that, because the duplicate would be silent for a filter pair.
        var services = CreateServices(services => services.AddFusion());
        var command = OperationCompletion.New(null, []);

        var handlers = services.GetRequiredService<CommandHandlerResolver>()
            .GetCommandHandlerChain(command);

        // The chain also carries every ICommand filter, so only the entries bound to this command
        // type are the ones at risk of being registered twice
        handlers.Items.Where(x => x.CommandType == typeof(OperationCompletion))
            .Should().HaveCount(1);
        // -2 is what the chain uses for "more than one non-filter handler"
        handlers.FinalHandlerIndex.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void AddOperationCompletionHandler_ReplacesTheRegisteredOne()
    {
        // CommandR registers the base handler through the same method, and Fusion then registers
        // its own - so calling it again must leave one handler, one listener and one alias target
        var services = CreateServices(services => {
            services.AddFusion();
            services.AddCommander().AddOperationCompletionHandler(c => new TestCompletionHandler(c));
        });

        services.GetRequiredService<OperationCompletionHandler>()
            .Should().BeOfType<TestCompletionHandler>();
        services.GetServices<IOperationCompletionListener>()
            .OfType<OperationCompletionHandler>()
            .Should().HaveCount(1);
        services.GetRequiredService<CommandHandlerResolver>()
            .GetCommandHandlerChain(OperationCompletion.New(null, []))
            .Items.Where(x => x.CommandType == typeof(OperationCompletion))
            .Should().HaveCount(1);
    }

    [Fact]
    public void AddOperationCompletionHandler_RoutesTheChainThroughTheBaseType()
    {
        // The chain entry resolves OperationCompletionHandler rather than the concrete type, which
        // is what makes the DI registration the seam an override replaces
        var services = CreateServices(services => {
            services.AddFusion();
            services.AddCommander().AddOperationCompletionHandler(c => new TestCompletionHandler(c));
        });

        var handler = services.GetRequiredService<CommandHandlerResolver>()
            .GetCommandHandlerChain(OperationCompletion.New(null, []))
            .Items.Single(x => x.CommandType == typeof(OperationCompletion));

        handler.GetHandlerServiceType().Should().Be(typeof(OperationCompletionHandler));
    }

    [Fact]
    public void FusionReplacesCommandRsDefaultHandler()
    {
        var services = CreateServices(services => services.AddFusion());

        services.GetRequiredService<OperationCompletionHandler>()
            .Should().BeOfType<FusionOperationCompletionHandler>();
    }

    // Nested types

    private sealed class TestCompletionHandler(IServiceProvider services)
        : FusionOperationCompletionHandler(services);
}
