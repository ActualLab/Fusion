using System.Reflection;
using ActualLab.CommandR.Rpc;
using ActualLab.Rpc;
using ActualLab.Rpc.Middlewares;
using ActualLab.Testing.Logging;
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
    public async Task RpcInboundCommandFilterMustNotBeEvaluatedForQueries()
    {
        var services = new ServiceCollection();
        services.AddCommander();
        services.AddRpc().AddServer<ITestRpcService, TestRpcService>();
        await using var serviceProvider = services.BuildServiceProvider();
        var methodDef = serviceProvider.RpcHub().ServiceRegistry
            .Get(typeof(ITestRpcService))!.Methods.Single(x => x.MethodInfo.Name == nameof(ITestRpcService.GetVersion));
        var context = new RpcMiddlewareContext<string>(methodDef);
        var invocationCount = 0;
        var handler = new RpcInboundCommandHandler() {
            Filter = _ => {
                invocationCount++;
                return true;
            },
        };

        handler.Create(context, _ => Task.FromResult(""));

        invocationCount.Should().Be(0);
    }

    [Fact]
    public async Task MalformedRpcCommandDiagnosticMustReportParameterCount()
    {
        var loggerProvider = new CapturingLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(logging => logging
            .ClearProviders()
            .SetMinimumLevel(LogLevel.Error)
            .AddProvider(loggerProvider));
        services.AddRpc().AddClient<IMalformedRpcCommandService>();
        await using var serviceProvider = services.BuildServiceProvider();
        var handler = new RpcCommandHandler(serviceProvider);
        var method = typeof(IMalformedRpcCommandService).GetMethod(nameof(IMalformedRpcCommandService.Run))!;
        var getRpcMethodDef = typeof(RpcCommandHandler).GetMethod(
            "GetRpcMethodDef",
            BindingFlags.Instance | BindingFlags.NonPublic)!;

        getRpcMethodDef.Invoke(handler, [typeof(IMalformedRpcCommandService), method]);

        loggerProvider.Content.Should().Contain("must have 2 parameters instead of 3");
    }

    // Nested types

    public interface IMalformedRpcCommandService : ICommandService
    {
        Task<int> Run(DiagnosticCommand command, int extra, CancellationToken cancellationToken);
    }

    public sealed record DiagnosticCommand : ICommand<int>;

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
}
