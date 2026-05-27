using ActualLab.Fusion.Tests.Services;
using ActualLab.Rpc;
using ActualLab.Rpc.Testing;
using ActualLab.Testing.Collections;

namespace ActualLab.Fusion.Tests.Rpc;

// Disconnect/Reconnect lifecycle matrix for Fusion compute method calls.
// See plans/sleepy-purring-porcupine.md for the matrix definition.
// Each [Fact] is one cell — kept separate so per-cell timing stays explicit.

[Collection(nameof(TimeSensitiveTests)), Trait("Category", nameof(TimeSensitiveTests))]
public class FusionRpcReconnectionMatrixTest(ITestOutputHelper @out) : SimpleFusionTestBase(@out)
{
    private static int _nextCallKey;

    protected override void ConfigureServices(ServiceCollection services)
    {
        base.ConfigureServices(services);
        var fusion = services.AddFusion();
        fusion.AddServerAndClient<IReconnectMatrixTester, ReconnectMatrixTester>();
    }

    // F1: DC@stage 0 (peer disconnected first) → RC@stage 1 (call sent on reconnect).
    // Expected: normal happy path; server invokes the body once.
    [Fact]
    public async Task F1_DisconnectBeforeSend_NormalFlow()
    {
        await using var services = CreateServices();
        var (client, server, connection) = Resolve(services);
        var callKey = NextCallKey();

        await connection.Disconnect();
        var task = client.Compute(callKey, 200, 200);
        await Delay(0.05);
        await connection.Connect();

        var result = await task.WaitAsync(TimeSpan.FromSeconds(3));
        result.Should().Be(callKey);

        await AssertServerInvocationCount(server, callKey, expected: 1);
        await AssertNoCalls(connection.ClientPeer, Out);
    }

    // F2: DC@stage 1 (call sent, body still running) → RC@stage 1 (body still S-Working).
    // Expected: $sys.Reconnect reports call known, no resend; one invocation.
    [Fact]
    public async Task F2_DisconnectMidExec_NoResend()
    {
        await using var services = CreateServices();
        var (client, server, connection) = Resolve(services);
        var callKey = NextCallKey();

        var task = client.Compute(callKey, 400, 400);
        await Delay(0.1); // call sent, body ~100ms in
        await connection.Disconnect();
        await Delay(0.05); // outage 50ms — server still S-Working at ~150ms
        await connection.Connect();

        var result = await task.WaitAsync(TimeSpan.FromSeconds(3));
        result.Should().Be(callKey);

        await AssertServerInvocationCount(server, callKey, expected: 1);
        await AssertNoCalls(connection.ClientPeer, Out);
    }

    // F3: DC@stage 1 (call sent) → RC@stage 2 (body finished during outage, invalidation not yet fired).
    // Expected: result + invalidation delivered via $sys.Reconnect; one invocation.
    [Fact]
    public async Task F3_DisconnectMidExec_ResultReadyDuringOutage()
    {
        await using var services = CreateServices();
        var (client, server, connection) = Resolve(services);
        var callKey = NextCallKey();

        var task = client.Compute(callKey, 200, 400);
        await Delay(0.05); // call sent, body ~50ms in
        await connection.Disconnect();
        await Delay(0.3); // outage 300ms — body finished (200ms) but invalidation not yet (would be at 600ms)
        await connection.Connect();

        var result = await task.WaitAsync(TimeSpan.FromSeconds(3));
        result.Should().Be(callKey);

        var computed = await Computed
            .Capture(() => client.Compute(callKey, 200, 400))
            .AsTask().WaitAsync(TimeSpan.FromSeconds(1));
        computed.IsConsistent().Should().BeTrue();
        await computed.WhenInvalidated().WaitAsync(TimeSpan.FromSeconds(3));

        await AssertServerInvocationCount(server, callKey, expected: 1);
        await AssertNoCalls(connection.ClientPeer, Out);
    }

    // F4: DC@stage 1 (call sent) → server fully invalidates during outage → RC@stage 1→3.
    // Observed: the original in-flight call resolves with its result (1st invocation).
    //   The client-side Computed was created in an invalidated state because server-side
    //   invalidation propagated during reconnect, so Computed.Capture re-fetches — that's
    //   the 2nd invocation. The fresh Computed then invalidates again on its own timer.
    [Fact]
    public async Task F4_DisconnectMidExec_InvalidatedDuringOutage()
    {
        await using var services = CreateServices();
        var (client, server, connection) = Resolve(services);
        var callKey = NextCallKey();

        var task = client.Compute(callKey, 100, 100);
        await Delay(0.03); // call sent, body ~30ms in
        await connection.Disconnect();
        await Delay(0.4); // outage 400ms — body done (100ms) + invalidation (200ms total) fired during outage
        await connection.Connect();

        var result = await task.WaitAsync(TimeSpan.FromSeconds(3));
        result.Should().Be(callKey);

        var computed = await Computed
            .Capture(() => client.Compute(callKey, 100, 100))
            .AsTask().WaitAsync(TimeSpan.FromSeconds(1));
        await computed.WhenInvalidated().WaitAsync(TimeSpan.FromSeconds(3));

        await AssertServerInvocationCount(server, callKey, expected: 2);
        await AssertNoCalls(connection.ClientPeer, Out);
    }

    // F5: DC@stage 2 (result received) → RC@stage 2 (server still S-ResultReady, no invalidation yet).
    // Expected: $sys.Reconnect reports compute call known → invalidation arrives when due; one invocation.
    [Fact]
    public async Task F5_DisconnectAfterResult_BeforeServerInvalidation()
    {
        await using var services = CreateServices();
        var (client, server, connection) = Resolve(services);
        var callKey = NextCallKey();

        var task = client.Compute(callKey, 200, 400);
        var result = await task.WaitAsync(TimeSpan.FromSeconds(3)); // ~200ms total
        result.Should().Be(callKey);
        var computed = await Computed
            .Capture(() => client.Compute(callKey, 200, 400))
            .AsTask().WaitAsync(TimeSpan.FromSeconds(1));
        computed.IsConsistent().Should().BeTrue();

        await connection.Disconnect();
        await Delay(0.1); // outage 100ms — server still S-ResultReady, no invalidation yet (would fire at 600ms)
        await connection.Connect();

        await computed.WhenInvalidated().WaitAsync(TimeSpan.FromSeconds(3));

        await AssertServerInvocationCount(server, callKey, expected: 1);
        await AssertNoCalls(connection.ClientPeer, Out);
    }

    // F6: DC@stage 2 (result received) → server invalidates during outage → RC@stage 2→3.
    // Expected: server forgot the call; on reconnect the client's reconcile path SetInvalidated()s
    //   locally for stage-2 compute calls — caller observes invalidation, no resend.
    [Fact]
    public async Task F6_DisconnectAfterResult_InvalidatedDuringOutage()
    {
        await using var services = CreateServices();
        var (client, server, connection) = Resolve(services);
        var callKey = NextCallKey();

        var task = client.Compute(callKey, 100, 100);
        var result = await task.WaitAsync(TimeSpan.FromSeconds(3)); // ~100ms
        result.Should().Be(callKey);
        var computed = await Computed
            .Capture(() => client.Compute(callKey, 100, 100))
            .AsTask().WaitAsync(TimeSpan.FromSeconds(1));
        computed.IsConsistent().Should().BeTrue();

        await connection.Disconnect();
        await Delay(0.3); // outage 300ms — server-side invalidation (200ms total) fires during outage
        await connection.Connect();

        await computed.WhenInvalidated().WaitAsync(TimeSpan.FromSeconds(3));

        await AssertServerInvocationCount(server, callKey, expected: 1);
        await AssertNoCalls(connection.ClientPeer, Out);
    }

    // Protected methods

    protected override ServiceProvider CreateServices(Action<IServiceCollection>? configureServices = null)
        => base.CreateServices(services => {
            configureServices?.Invoke(services);
        });

    // Private methods

    private static int NextCallKey()
        => Interlocked.Increment(ref _nextCallKey);

    private static (IReconnectMatrixTester Client, ReconnectMatrixTester Server, RpcTestConnection Connection) Resolve(
        IServiceProvider services)
    {
        var connection = services.GetRequiredService<RpcTestClient>().GetConnection(x => !x.IsBackend);
        var client = services.RpcHub().GetClient<IReconnectMatrixTester>();
        var server = services.GetRequiredService<ReconnectMatrixTester>();
        return (client, server, connection);
    }

    private static async Task AssertServerInvocationCount(
        ReconnectMatrixTester server, int callKey, int expected)
    {
        var actual = await server.GetComputeInvocationCount(callKey);
        actual.Should().Be(expected,
            $"server-side invocation count for callKey={callKey} should be {expected}");
    }
}
