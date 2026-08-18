using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Testing;

namespace ActualLab.Tests.Rpc;

public class RpcRerouteCancellationTest(ITestOutputHelper @out) : RpcLocalTestBase(@out)
{
    private readonly TestReroutableRef _clientRef = new();
    private readonly RpcRef _serverRef = RpcRef.NewServer("test-reroutable", RpcTestBase.DefaultSerializationFormat);
    private RpcRef? _rerouteTarget;

    protected override void ConfigureServices(ServiceCollection services)
    {
        base.ConfigureServices(services);
        var rpc = services.AddRpc();
        rpc.AddServerAndClient<ITestRerouteService, TestRerouteService>();
        services.AddSingleton<TestRerouteState>();
        services.AddSingleton<RpcOutboundCallOptions>(_ => RpcOutboundCallOptions.Default with {
            RouterFactory = _ => _ => Volatile.Read(ref _rerouteTarget) ?? _clientRef,
        });
    }

    protected override void StartServices(IServiceProvider services)
    {
        var testClient = services.GetRequiredService<RpcTestClient>();
        _ = testClient.CreateConnection(_clientRef, _serverRef).Connect();
    }

    [Fact]
    public async Task RerouteCancelsTheCallOnTheOldPeerTest()
    {
        await using var services = CreateServices();
        var state = services.GetRequiredService<TestRerouteState>();
        var client = services.RpcHub().GetClient<ITestRerouteService>();

        var callTask = client.Run(42, CancellationToken.None);
        await state.WhenStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        callTask.IsCompleted.Should().BeFalse();

        // The reroute target has no test connection, so it can never take the old peer's
        // connection down: $sys.Cancel is the only thing that can end the running execution
        Volatile.Write(ref _rerouteTarget, RpcRef.NewClient("test-reroute-nowhere"));
        _clientRef.Reset();

        await TestExt.When(
            () => state.CancelCount.Should().Be(1),
            TimeSpan.FromSeconds(5));
        state.StartCount.Should().Be(1);
    }

    // Nested types

    private sealed class TestReroutableRef : RpcRef
    {
        public TestReroutableRef()
        {
            HostInfo = "test-reroutable";
            UseReferentialEquality = true;
            Initialize();
        }

        protected override RpcRoute CreateRoute()
            => new(this);
    }
}

public interface ITestRerouteService : IRpcService
{
    public Task<int> Run(int input, CancellationToken cancellationToken = default);
}

public class TestRerouteState
{
    private int _startCount;
    private int _cancelCount;

    public readonly TaskCompletionSource<Unit> WhenStarted = TaskCompletionSourceExt.New<Unit>();
    public TimeSpan CallDuration { get; init; } = TimeSpan.FromSeconds(30);

    public int StartCount => Volatile.Read(ref _startCount);
    public int CancelCount => Volatile.Read(ref _cancelCount);

    public void RegisterStart()
    {
        Interlocked.Increment(ref _startCount);
        WhenStarted.TrySetResult(default);
    }

    public void RegisterCancellation()
        => Interlocked.Increment(ref _cancelCount);
}

public class TestRerouteService(TestRerouteState state) : ITestRerouteService
{
    public virtual async Task<int> Run(int input, CancellationToken cancellationToken = default)
    {
        state.RegisterStart();
        try {
            await Task.Delay(state.CallDuration, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) {
            state.RegisterCancellation();
            throw;
        }
        return input;
    }
}
