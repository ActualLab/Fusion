using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Testing;

namespace ActualLab.Tests.Rpc;

// Reconnect processing applies only to calls actually sent on the connection that
// just died. AllowReconnect / AllowResend describe what happens to an in-flight
// call when the link breaks; a call still waiting for a connection has nothing to
// reconcile, and sweeping it rejects what that same reconnect just sent.
public class RpcUnsentCallTest(ITestOutputHelper @out) : RpcLocalTestBase(@out)
{
    protected override void ConfigureServices(ServiceCollection services)
    {
        base.ConfigureServices(services);
        var rpc = services.AddRpc();
        rpc.AddServerAndClient<ITestUnsentCallService, TestUnsentCallService>();
    }

    [Fact]
    public async Task QueuedAwaitOnlyCallSurvivesReconnect()
    {
        await using var services = CreateServices();
        var connection = services.GetRequiredService<RpcTestClient>().GetConnection(x => !x.IsBackend);
        var server = services.GetRequiredService<TestUnsentCallService>();
        var client = services.RpcHub().GetClient<ITestUnsentCallService>();
        await client.AwaitOnlyPing("warmup"); // Ensures the peer has handshaked at least once

        await connection.Disconnect();
        // The handler is slow on purpose: the call has to still be in flight while
        // reconnect processing runs, or an instant reply makes the sweep a silent
        // no-op and the test passes even when the sweep is wrong.
        var task = client.AwaitOnlySlowPing("p1", TimeSpan.FromMilliseconds(200));
        await Delay(0.05);
        task.IsCompleted.Should().BeFalse(); // Queued, never sent

        await connection.Connect();
        (await task).Should().Be(1);
        server.GetCallCount("p1").Should().Be(1);
        await AssertNoCalls(connection.ClientPeer, Out);
    }

    [Fact]
    public async Task QueuedReconnectModeCallIsExecutedOnce()
    {
        await using var services = CreateServices();
        var connection = services.GetRequiredService<RpcTestClient>().GetConnection(x => !x.IsBackend);
        var server = services.GetRequiredService<TestUnsentCallService>();
        var client = services.RpcHub().GetClient<ITestUnsentCallService>();
        await client.AwaitOnlyPing("warmup");

        await connection.Disconnect();
        var task = client.ReconnectPing("p1");
        await Delay(0.05);
        task.IsCompleted.Should().BeFalse();

        // The reconnect flushes the queued call; reconnect processing must not
        // treat it as an in-flight survivor and resend it on top of that.
        await connection.Connect();
        await task;
        await Delay(0.1);
        server.GetCallCount("p1").Should().Be(1);
        await AssertNoCalls(connection.ClientPeer, Out);
    }

    [Fact]
    public async Task QueuedAwaitOnlyCallsSurviveConnectionChurn()
    {
        await using var services = CreateServices();
        var connection = services.GetRequiredService<RpcTestClient>().GetConnection(x => !x.IsBackend);
        var server = services.GetRequiredService<TestUnsentCallService>();
        var client = services.RpcHub().GetClient<ITestUnsentCallService>();
        await client.AwaitOnlyPing("warmup");

        // Each call is issued while Connect() is already in flight, so it lands on
        // either side of SetConnectionState - the snapshot must exclude it whichever
        // way the race goes, or this fails intermittently.
        for (var i = 0; i < 20; i++) {
            await connection.Disconnect();
            var connectTask = connection.Connect();
            var id = $"p{i}";
            var task = client.AwaitOnlySlowPing(id, TimeSpan.FromMilliseconds(100));
            await connectTask;
            (await task).Should().Be(1);
            server.GetCallCount(id).Should().Be(1);
        }
        await AssertNoCalls(connection.ClientPeer, Out);
    }

    [Fact]
    public async Task InFlightAwaitOnlyCallIsStillAbortedOnReconnect()
    {
        await using var services = CreateServices();
        var connection = services.GetRequiredService<RpcTestClient>().GetConnection(x => !x.IsBackend);
        var client = services.RpcHub().GetClient<ITestUnsentCallService>();
        await client.AwaitOnlyPing("warmup");

        // Sent on this connection, still awaiting its result when the link breaks.
        var task = client.AwaitOnlyDelay(TimeSpan.FromMilliseconds(500));
        await Delay(0.05);
        await connection.Disconnect();
        await Delay(0.02);
        await connection.Connect();

        var error = await Assert.ThrowsAsync<RpcException>(() => task);
        error.Message.Should().Contain("AllowReconnect");
        await AssertNoCalls(connection.ClientPeer, Out);
    }
}

// The same invariant across a peer change. RpcTestConnection binds to one server
// peer for life, so this drives the client through two connections sharing a client
// peer ref but not a server peer ref - the second handshake then reports Changed.
public class RpcUnsentCallPeerChangeTest(ITestOutputHelper @out) : RpcLocalTestBase(@out)
{
    protected override void ConfigureServices(ServiceCollection services)
    {
        base.ConfigureServices(services);
        var rpc = services.AddRpc();
        rpc.AddServerAndClient<ITestUnsentCallService, TestUnsentCallService>();

        // AddTestClient aliases RpcClient -> RpcTestClient, so replacing the
        // RpcTestClient registration is enough to redirect the whole chain.
        services.RemoveAll(d => d.ServiceType == typeof(RpcTestClient));
        services.AddSingleton(c => new SwitchableRpcTestClient(c));
        services.AddAlias<RpcTestClient, SwitchableRpcTestClient>();
    }

    protected override void StartServices(IServiceProvider services)
    {
        // The test builds and switches its own connections.
    }

    [Fact]
    public async Task QueuedAwaitOnlyCallSurvivesPeerChange()
    {
        await using var services = CreateServices();
        var testClient = services.GetRequiredService<SwitchableRpcTestClient>();
        var server = services.GetRequiredService<TestUnsentCallService>();
        var client = services.RpcHub().GetClient<ITestUnsentCallService>();

        var clientPeerRef = RpcRef.Default;
        var connection1 = new RpcTestConnection(testClient, clientPeerRef, RpcRef.NewServer("server-1"));
        var connection2 = new RpcTestConnection(testClient, clientPeerRef, RpcRef.NewServer("server-2"));
        connection1.ServerPeer.Id.Should().NotBe(connection2.ServerPeer.Id);

        testClient.Connection = connection1;
        await connection1.Connect();
        await client.AwaitOnlyPing("warmup");

        await connection1.Disconnect();
        testClient.Connection = connection2;

        // AwaitForConnection | !AllowResend, queued while down: it never reached
        // server-1, so the switch to server-2 must send it, not abort it.
        var task = client.AwaitOnlySlowPing("p1", TimeSpan.FromMilliseconds(200));
        await Delay(0.05);
        task.IsCompleted.Should().BeFalse();

        await connection2.Connect();
        (await task).Should().Be(1);
        server.GetCallCount("p1").Should().Be(1);

        var clientPeer = connection1.ClientPeer;
        clientPeer.ConnectionState.Value.Handshake!.RemotePeerId
            .Should().Be(connection2.ServerPeer.Id); // The peer really did change
        await AssertNoCalls(clientPeer, Out);
    }

}

public interface ITestUnsentCallService : IRpcService
{
    [RpcMethod(RemoteExecutionMode = RpcRemoteExecutionMode.AwaitForConnection)]
    public Task<int> AwaitOnlyPing(string id, CancellationToken cancellationToken = default);

    [RpcMethod(RemoteExecutionMode = RpcRemoteExecutionMode.AwaitForConnection)]
    public Task<int> AwaitOnlySlowPing(string id, TimeSpan duration, CancellationToken cancellationToken = default);

    [RpcMethod(RemoteExecutionMode = RpcRemoteExecutionMode.AwaitForConnection)]
    public Task<TimeSpan> AwaitOnlyDelay(TimeSpan duration, CancellationToken cancellationToken = default);

    [RpcMethod(RemoteExecutionMode = RpcRemoteExecutionMode.AwaitForConnection | RpcRemoteExecutionMode.AllowReconnect)]
    public Task<int> ReconnectPing(string id, CancellationToken cancellationToken = default);
}

public class TestUnsentCallService : ITestUnsentCallService
{
    private readonly ConcurrentDictionary<string, int> _callCounts = new(StringComparer.Ordinal);

    public int GetCallCount(string id)
        => _callCounts.GetValueOrDefault(id);

    public Task<int> AwaitOnlyPing(string id, CancellationToken cancellationToken = default)
        => Task.FromResult(Count(id));

    public async Task<int> AwaitOnlySlowPing(
        string id, TimeSpan duration, CancellationToken cancellationToken = default)
    {
        var count = Count(id);
        await Task.Delay(duration, cancellationToken);
        return count;
    }

    public async Task<TimeSpan> AwaitOnlyDelay(TimeSpan duration, CancellationToken cancellationToken = default)
    {
        await Task.Delay(duration, cancellationToken);
        return duration;
    }

    public Task<int> ReconnectPing(string id, CancellationToken cancellationToken = default)
        => Task.FromResult(Count(id));

    private int Count(string id)
        => _callCounts.AddOrUpdate(id, 1, static (_, count) => count + 1);
}
