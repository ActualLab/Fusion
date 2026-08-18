using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Tests.Rpc;

public class RpcRouteLocalExecutionTest(ITestOutputHelper @out) : RpcLocalTestBase(@out)
{
    private readonly TestLocalExecRef _rpcRef = new();

    protected override void ConfigureServices(ServiceCollection services)
    {
        base.ConfigureServices(services);
        var rpc = services.AddRpc();
        rpc.AddDistributedService<ITestLocalExecService, TestLocalExecService>();
        services.AddSingleton<TestLocalExecState>();
        services.AddSingleton<RpcOutboundCallOptions>(_ => RpcOutboundCallOptions.Default with {
            RouterFactory = _ => _ => _rpcRef,
        });
    }

    [Fact]
    public async Task ConstrainedModeLinksChangedTokenOnSyncPathTest()
    {
        await using var services = CreateServices();
        var hub = services.RpcHub();
        var methodDef = hub.ServiceRegistry[typeof(ITestLocalExecService)]["Run:2"];
        methodDef.LocalExecutionMode.Should().Be(RpcLocalExecutionMode.Constrained);

        var route = _rpcRef.Route;
        var whenReadyTask = route.LocalExecutionAwaiter!.Invoke(false, CancellationToken.None);
        whenReadyTask.IsCompletedSuccessfully.Should().BeTrue(); // The test awaiter must use the sync path

        var linkedCts = await route.PrepareLocalExecution(methodDef, addDependency: false, CancellationToken.None);
        linkedCts.Should().NotBeNull();
        linkedCts!.Token.IsCancellationRequested.Should().BeFalse();

        route.MarkChanged();
        linkedCts.Token.IsCancellationRequested.Should().BeTrue();
        linkedCts.CancelAndDisposeSilently();
    }

    [Fact]
    public async Task ConstrainedEntryModeHasNoChangedTokenOnSyncPathTest()
    {
        await using var services = CreateServices();
        var hub = services.RpcHub();
        var methodDef = hub.ServiceRegistry[typeof(ITestLocalExecService)]["RunEntry:2"];
        methodDef.LocalExecutionMode.Should().Be(RpcLocalExecutionMode.ConstrainedEntry);

        var route = _rpcRef.Route;
        var linkedCts = await route.PrepareLocalExecution(methodDef, addDependency: false, CancellationToken.None);
        linkedCts.Should().BeNull(); // ConstrainedEntry never links the route's ChangedToken

        route.MarkChanged();
        Assert.Throws<RpcRerouteException>(
            () => route.PrepareLocalExecution(methodDef, addDependency: false, CancellationToken.None));
    }

    [Fact]
    public async Task ConstrainedCallIsAbortedOnRouteChangeTest()
    {
        await using var services = CreateServices();
        var state = services.GetRequiredService<TestLocalExecState>();
        var client = services.RpcHub().GetClient<ITestLocalExecService>();

        var callTask = client.Run(42, CancellationToken.None);
        await state.WhenStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        callTask.IsCompleted.Should().BeFalse();

        _rpcRef.Reset(); // Route change: the in-flight local execution must be aborted, then rerouted

        (await callTask.WaitAsync(TimeSpan.FromSeconds(10))).Should().Be(42);
        state.CancelCount.Should().Be(1);
        state.StartCount.Should().Be(2); // The aborted attempt + the rerouted one
    }

    // Nested types

    private sealed class TestLocalExecRef : RpcRef
    {
        public TestLocalExecRef()
        {
            HostInfo = "test-local-exec-ref";
            UseReferentialEquality = true;
            Initialize();
        }

        protected override RpcRoute CreateRoute()
            => new(this) {
                ConnectionKind = RpcPeerConnectionKind.Local,
                // Always ready => PrepareLocalExecution takes its synchronous fast path
                LocalExecutionAwaiter = static (_, _) => default,
            };
    }
}

public interface ITestLocalExecService : IRpcService
{
    [RpcMethod(LocalExecutionMode = RpcLocalExecutionMode.Constrained)]
    public Task<int> Run(int input, CancellationToken cancellationToken = default);

    [RpcMethod(LocalExecutionMode = RpcLocalExecutionMode.ConstrainedEntry)]
    public Task<int> RunEntry(int input, CancellationToken cancellationToken = default);
}

public class TestLocalExecState
{
    private int _startCount;
    private int _cancelCount;

    public readonly TaskCompletionSource<Unit> WhenStarted = TaskCompletionSourceExt.New<Unit>();
    public TimeSpan FirstCallDuration { get; init; } = TimeSpan.FromSeconds(30);

    public int StartCount => Volatile.Read(ref _startCount);
    public int CancelCount => Volatile.Read(ref _cancelCount);

    public int NextStartIndex()
    {
        var index = Interlocked.Increment(ref _startCount);
        WhenStarted.TrySetResult(default);
        return index;
    }

    public void RegisterCancellation()
        => Interlocked.Increment(ref _cancelCount);
}

public class TestLocalExecService(TestLocalExecState state) : ITestLocalExecService
{
    public virtual async Task<int> Run(int input, CancellationToken cancellationToken = default)
    {
        // Only the first attempt blocks - so an un-aborted one hangs the test rather than passing it
        if (state.NextStartIndex() != 1)
            return input;

        try {
            await Task.Delay(state.FirstCallDuration, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) {
            state.RegisterCancellation();
            throw;
        }
        return input;
    }

    public virtual Task<int> RunEntry(int input, CancellationToken cancellationToken = default)
        => Task.FromResult(input);
}
