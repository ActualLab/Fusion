using ActualLab.CommandR.Rpc;
using ActualLab.Rpc;
using ActualLab.Rpc.Middlewares;
using ActualLab.Tests.Rpc;

namespace ActualLab.Tests.CommandR;

public class CommandRExecutionAuditTest
{
    [Fact]
    public async Task CommandScopeMustBeDisposedOnlyOnce()
    {
        var services = new ServiceCollection();
        services.AddCommander();
        services.AddScoped<DisposalTracker>();
        await using var serviceProvider = services.BuildServiceProvider();
        var tracker = await serviceProvider.Commander().Call(LocalCommand.New<int>((context, _) => {
            var scopedTracker = context.Services.GetRequiredService<DisposalTracker>();
            scopedTracker.WasResolved = true;
            return scopedTracker.AsyncDisposeCount + scopedTracker.DisposeCount;
        }));

        tracker.Should().Be(0);
        var disposalTracker = DisposalTracker.LastResolved!;
        disposalTracker.WasResolved.Should().BeTrue();
        disposalTracker.AsyncDisposeCount.Should().Be(1);
        disposalTracker.DisposeCount.Should().Be(0);
    }

    [Fact]
    public async Task RpcInboundCommandFilterMustBeEvaluatedOnce()
    {
        var services = new ServiceCollection();
        services.AddCommander();
        services.AddRpc().AddServer<ITestRpcService, TestRpcService>();
        await using var serviceProvider = services.BuildServiceProvider();
        var methodDef = serviceProvider.RpcHub().ServiceRegistry
            .Get(typeof(ITestRpcService))!.Methods.Single(x => x.Kind == RpcMethodKind.Command);
        var context = new RpcMiddlewareContext<string>(methodDef);
        var invocationCount = 0;
        var handler = new RpcInboundCommandHandler() {
            Filter = _ => {
                invocationCount++;
                return true;
            },
        };

        handler.Create(context, _ => Task.FromResult(""));

        invocationCount.Should().Be(1);
    }

    [Fact]
    public void CommandHandlerChainMustOwnItsHandlerStorage()
    {
        var finalHandler = InterfaceCommandHandler.New<FinalHandler, StorageCommand>();
        var filter = InterfaceCommandHandler.New<FilterHandler, StorageCommand>(isFilter: true);
        var handlers = new CommandHandler[] { finalHandler };
        var chain = new CommandHandlerChain(handlers);

        handlers[0] = filter;

        chain.FinalHandler.Should().BeSameAs(finalHandler);
    }

    private sealed class DisposalTracker : IDisposable, IAsyncDisposable
    {
        public static DisposalTracker? LastResolved { get; private set; }

        public bool WasResolved {
            get;
            set {
                field = value;
                LastResolved = this;
            }
        }
        public int DisposeCount { get; private set; }
        public int AsyncDisposeCount { get; private set; }

        public void Dispose()
            => DisposeCount++;

        public ValueTask DisposeAsync()
        {
            AsyncDisposeCount++;
            return default;
        }
    }

    private sealed record StorageCommand : ICommand<Unit>;
    private sealed class FinalHandler;
    private sealed class FilterHandler;
}
