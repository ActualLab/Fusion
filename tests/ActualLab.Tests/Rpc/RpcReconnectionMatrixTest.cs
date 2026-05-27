using ActualLab.Rpc;
using ActualLab.Rpc.Testing;
using ActualLab.Testing.Collections;

namespace ActualLab.Tests.Rpc;

// Disconnect/Reconnect lifecycle matrix for regular (non-Fusion) RPC calls.
// See plans/sleepy-purring-porcupine.md for the matrix definition.

[Collection(nameof(TimeSensitiveTests)), Trait("Category", nameof(TimeSensitiveTests))]
public class RpcReconnectionMatrixTest(ITestOutputHelper @out) : RpcLocalTestBase(@out)
{
    private static int _nextCallKey;

    protected override void ConfigureServices(ServiceCollection services)
    {
        base.ConfigureServices(services);
        var rpc = services.AddRpc();
        rpc.AddServerAndClient<IReconnectMatrixRpcTester, ReconnectMatrixRpcTester>();
    }

    // R1: DC@stage 0 (peer disconnected first) → RC@stage 1 (call sent on reconnect).
    [Fact]
    public async Task R1_DisconnectBeforeSend_NormalFlow()
    {
        await using var services = CreateServices();
        var (client, server, connection) = Resolve(services);
        var callKey = NextCallKey();

        await connection.Disconnect();
        var task = client.Rpc(callKey, 200);
        await Delay(0.05);
        await connection.Connect();

        var result = await task.WaitAsync(TimeSpan.FromSeconds(3));
        result.Should().Be(callKey);

        await AssertInvocationCount(server, callKey, expected: 1);
        await AssertNoCalls(connection.ClientPeer, Out);
    }

    // R2: DC@stage 1 (call sent, body still running) → RC@stage 1 (S-Working).
    // Expected: $sys.Reconnect known → no resend; one invocation.
    [Fact]
    public async Task R2_DisconnectMidExec_NoResend()
    {
        await using var services = CreateServices();
        var (client, server, connection) = Resolve(services);
        var callKey = NextCallKey();

        var task = client.Rpc(callKey, 400);
        await Delay(0.1); // call sent, ~100ms into body
        await connection.Disconnect();
        await Delay(0.05); // outage 50ms — still S-Working at ~150ms
        await connection.Connect();

        var result = await task.WaitAsync(TimeSpan.FromSeconds(3));
        result.Should().Be(callKey);

        await AssertInvocationCount(server, callKey, expected: 1);
        await AssertNoCalls(connection.ClientPeer, Out);
    }

    // R3: DC@stage 1 → server finishes body during outage → RC.
    // Regular RPC calls unregister from the inbound tracker after body completion
    //   regardless of whether the result was actually delivered. On reconnect,
    //   $sys.Reconnect says "unknown" → client resends → fresh execution; count = 2.
    [Fact]
    public async Task R3_DisconnectMidExec_BodyCompletedDuringOutage()
    {
        await using var services = CreateServices();
        var (client, server, connection) = Resolve(services);
        var callKey = NextCallKey();

        var task = client.Rpc(callKey, 100);
        await Delay(0.03); // call sent, body ~30ms in
        await connection.Disconnect();
        await Delay(0.2); // outage 200ms — body done (100ms) but result couldn't be delivered
        await connection.Connect();

        var result = await task.WaitAsync(TimeSpan.FromSeconds(3));
        result.Should().Be(callKey);

        await AssertInvocationCount(server, callKey, expected: 2);
        await AssertNoCalls(connection.ClientPeer, Out);
    }

    // Private methods

    private static int NextCallKey()
        => Interlocked.Increment(ref _nextCallKey);

    private static (IReconnectMatrixRpcTester Client, ReconnectMatrixRpcTester Server, RpcTestConnection Connection) Resolve(
        IServiceProvider services)
    {
        var connection = services.GetRequiredService<RpcTestClient>().GetConnection(x => !x.IsBackend);
        var client = services.RpcHub().GetClient<IReconnectMatrixRpcTester>();
        var server = services.GetRequiredService<ReconnectMatrixRpcTester>();
        return (client, server, connection);
    }

    private static async Task AssertInvocationCount(
        ReconnectMatrixRpcTester server, int callKey, int expected)
    {
        var actual = await server.GetInvocationCount(callKey);
        actual.Should().Be(expected,
            $"server-side invocation count for callKey={callKey} should be {expected}");
    }
}
