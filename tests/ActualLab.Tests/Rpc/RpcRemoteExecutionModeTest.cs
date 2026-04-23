using ActualLab.Rpc;
using ActualLab.Rpc.Testing;
using ActualLab.Testing.Collections;

namespace ActualLab.Tests.Rpc;

[Collection(nameof(TimeSensitiveTests)), Trait("Category", nameof(TimeSensitiveTests))]
public class RpcRemoteExecutionModeTest(ITestOutputHelper @out) : RpcLocalTestBase(@out)
{
    protected override void ConfigureServices(ServiceCollection services)
    {
        base.ConfigureServices(services);
        var rpc = services.AddRpc();
        rpc.AddServerAndClient<ITestRemoteExecService, TestRemoteExecService>();
        services.AddSingleton<RpcPeerOptions>(_ => RpcPeerOptions.Default with {
            UseRandomHandshakeIndex = true,
            PeerFactory = (hub, peerRef) => peerRef.IsServer
                ? new RpcServerPeer(hub, peerRef)
                : new RpcClientPeer(hub, peerRef),
        });
    }

    [Fact]
    public async Task AttributeTest()
    {
        await using var services = CreateServices();
        var hub = services.RpcHub();
        var serviceDef = hub.ServiceRegistry[typeof(ITestRemoteExecService)];

        // Methods with explicit attributes
        serviceDef["NoneDelay:2"].RemoteExecutionMode.Should().Be((RpcRemoteExecutionMode)0);
        serviceDef["AwaitOnlyDelay:2"].RemoteExecutionMode.Should().Be(RpcRemoteExecutionMode.AwaitForConnection);
        serviceDef["ReconnectDelay:2"].RemoteExecutionMode.Should().Be(
            RpcRemoteExecutionMode.AwaitForConnection | RpcRemoteExecutionMode.AllowReconnect);

        // Default method
        serviceDef["DefaultDelay:2"].RemoteExecutionMode.Should().Be(RpcRemoteExecutionMode.Default);

        // NoWait method always gets 0
        serviceDef["NoWaitSet:2"].RemoteExecutionMode.Should().Be((RpcRemoteExecutionMode)0);
    }

    [Fact]
    public async Task NoneMode_WorksWhenConnected()
    {
        await using var services = CreateServices();
        var client = services.RpcHub().GetClient<ITestRemoteExecService>();
        await client.DefaultDelay(TimeSpan.FromMilliseconds(1)); // Warm up — ensures connection is ready

        var result = await client.NoneDelay(TimeSpan.FromMilliseconds(10));
        result.Should().Be(TimeSpan.FromMilliseconds(10));
    }

    [Fact]
    public async Task NoneMode_FailsWhenDisconnected()
    {
        await using var services = CreateServices();
        var connection = services.GetRequiredService<RpcTestClient>().GetConnection(x => !x.IsBackend);
        var client = services.RpcHub().GetClient<ITestRemoteExecService>();
        await client.DefaultDelay(TimeSpan.FromMilliseconds(1)); // Warm up

        await connection.Disconnect();

        // Should fail immediately with RpcException, not hang
        var ex = await Assert.ThrowsAsync<RpcException>(
            () => client.NoneDelay(TimeSpan.FromMilliseconds(10)));
        ex.Message.Should().Contain("AwaitForConnection");
    }

    [Fact]
    public async Task NoneMode_InFlightCallAbortedOnReconnect()
    {
        await using var services = CreateServices();
        var connection = services.GetRequiredService<RpcTestClient>().GetConnection(x => !x.IsBackend);
        var clientPeer = connection.ClientPeer;
        var client = services.RpcHub().GetClient<ITestRemoteExecService>();
        await client.DefaultDelay(TimeSpan.FromMilliseconds(1)); // Warm up

        // Start a slow call, then disconnect and reconnect
        var task = client.NoneDelay(TimeSpan.FromMilliseconds(500));
        await Delay(0.02); // Let the call start
        await connection.Disconnect();
        await Delay(0.02);
        await connection.Connect();

        // The call should fail because AllowReconnect is not set
        await Assert.ThrowsAsync<RpcException>(() => task);
        await AssertNoCalls(clientPeer, Out);
    }

    [Fact]
    public async Task AwaitOnly_WaitsButAbortsOnReconnect()
    {
        await using var services = CreateServices();
        var connection = services.GetRequiredService<RpcTestClient>().GetConnection(x => !x.IsBackend);
        var clientPeer = connection.ClientPeer;
        var client = services.RpcHub().GetClient<ITestRemoteExecService>();
        await client.DefaultDelay(TimeSpan.FromMilliseconds(1)); // Warm up

        await connection.Disconnect();

        // Start call while disconnected — it should wait (AwaitForConnection is set)
        var task = client.AwaitOnlyDelay(TimeSpan.FromMilliseconds(500));
        await Delay(0.05);
        task.IsCompleted.Should().BeFalse(); // Still waiting for connection

        // Reconnect triggers Reconnect() which aborts it (no AllowReconnect)
        await connection.Connect();
        await Assert.ThrowsAsync<RpcException>(() => task);
        await AssertNoCalls(clientPeer, Out);
    }

    [Fact]
    public async Task AwaitOnly_AbortedOnReconnect()
    {
        await using var services = CreateServices();
        var connection = services.GetRequiredService<RpcTestClient>().GetConnection(x => !x.IsBackend);
        var clientPeer = connection.ClientPeer;
        var client = services.RpcHub().GetClient<ITestRemoteExecService>();
        await client.DefaultDelay(TimeSpan.FromMilliseconds(1)); // Warm up

        // Start a slow call, then disconnect and reconnect
        var task = client.AwaitOnlyDelay(TimeSpan.FromMilliseconds(500));
        await Delay(0.02);
        await connection.Disconnect();
        await Delay(0.02);
        await connection.Connect();

        // Should fail — AllowReconnect is not set
        await Assert.ThrowsAsync<RpcException>(() => task);
        await AssertNoCalls(clientPeer, Out);
    }

    [Fact]
    public async Task Reconnect_SurvivesReconnectToSamePeer()
    {
        await using var services = CreateServices();
        var connection = services.GetRequiredService<RpcTestClient>().GetConnection(x => !x.IsBackend);
        var clientPeer = connection.ClientPeer;
        var client = services.RpcHub().GetClient<ITestRemoteExecService>();
        await client.DefaultDelay(TimeSpan.FromMilliseconds(1)); // Warm up

        // Start a slow call, then disconnect and reconnect (same peer)
        var task = client.ReconnectDelay(TimeSpan.FromMilliseconds(200));
        await Delay(0.02);
        await connection.Disconnect();
        await Delay(0.02);
        await connection.Connect();

        // Should succeed — AllowReconnect is set
        var result = await task;
        result.Should().Be(TimeSpan.FromMilliseconds(200));
        await AssertNoCalls(clientPeer, Out);
    }

    [Fact]
    public async Task Default_SurvivesReconnect()
    {
        await using var services = CreateServices();
        var connection = services.GetRequiredService<RpcTestClient>().GetConnection(x => !x.IsBackend);
        var clientPeer = connection.ClientPeer;
        var client = services.RpcHub().GetClient<ITestRemoteExecService>();
        await client.DefaultDelay(TimeSpan.FromMilliseconds(1)); // Warm up

        var task = client.DefaultDelay(TimeSpan.FromMilliseconds(200));
        await Delay(0.02);
        await connection.Disconnect();
        await Delay(0.02);
        await connection.Connect();

        var result = await task;
        result.Should().Be(TimeSpan.FromMilliseconds(200));
        await AssertNoCalls(clientPeer, Out);
    }

    [Fact]
    public async Task ConcurrentDisruption_NoneMode()
    {
        await using var services = CreateServices();
        var connection = services.GetRequiredService<RpcTestClient>().GetConnection(x => !x.IsBackend);
        var clientPeer = connection.ClientPeer;
        var client = services.RpcHub().GetClient<ITestRemoteExecService>();
        await client.DefaultDelay(TimeSpan.FromMilliseconds(1)); // Warm up

        var disruptorCts = new CancellationTokenSource();
        var disruptorTask = ConnectionDisruptor("default", connection, disruptorCts.Token);

        var successCount = 0;
        var failCount = 0;
        const int iterations = 50;

        var tasks = Enumerable.Range(0, iterations).Select(async _ => {
            try {
                await client.NoneDelay(TimeSpan.FromMilliseconds(5), CancellationToken.None);
                Interlocked.Increment(ref successCount);
            }
            catch {
                Interlocked.Increment(ref failCount);
            }
        });

        await Task.WhenAll(tasks);

        disruptorCts.CancelAndDisposeSilently();
        await disruptorTask;

        Out.WriteLine($"Success: {successCount}, Fail: {failCount}");
        // Some should succeed, some should fail — no hangs
        (successCount + failCount).Should().Be(iterations);
        await AssertNoCalls(clientPeer, Out);
    }
}

// Test service interface with various RemoteExecutionMode settings
public interface ITestRemoteExecService : IRpcService
{
    [RpcMethod(RemoteExecutionMode = (RpcRemoteExecutionMode)0)]
    public Task<TimeSpan> NoneDelay(TimeSpan duration, CancellationToken cancellationToken = default);

    [RpcMethod(RemoteExecutionMode = RpcRemoteExecutionMode.AwaitForConnection)]
    public Task<TimeSpan> AwaitOnlyDelay(TimeSpan duration, CancellationToken cancellationToken = default);

    [RpcMethod(RemoteExecutionMode = RpcRemoteExecutionMode.AwaitForConnection | RpcRemoteExecutionMode.AllowReconnect)]
    public Task<TimeSpan> ReconnectDelay(TimeSpan duration, CancellationToken cancellationToken = default);

    public Task<TimeSpan> DefaultDelay(TimeSpan duration, CancellationToken cancellationToken = default);

    public ValueTask<RpcNoWait> NoWaitSet(string key, string? value);
}

public class TestRemoteExecService : ITestRemoteExecService
{
    public async Task<TimeSpan> NoneDelay(TimeSpan duration, CancellationToken cancellationToken = default)
    {
        await Task.Delay(duration, cancellationToken);
        return duration;
    }

    public async Task<TimeSpan> AwaitOnlyDelay(TimeSpan duration, CancellationToken cancellationToken = default)
    {
        await Task.Delay(duration, cancellationToken);
        return duration;
    }

    public async Task<TimeSpan> ReconnectDelay(TimeSpan duration, CancellationToken cancellationToken = default)
    {
        await Task.Delay(duration, cancellationToken);
        return duration;
    }

    public async Task<TimeSpan> DefaultDelay(TimeSpan duration, CancellationToken cancellationToken = default)
    {
        await Task.Delay(duration, cancellationToken);
        return duration;
    }

    public ValueTask<RpcNoWait> NoWaitSet(string key, string? value) => default;
}
