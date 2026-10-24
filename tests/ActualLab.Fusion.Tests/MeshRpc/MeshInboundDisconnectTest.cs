using ActualLab.Net;
using ActualLab.Rpc;
using ActualLab.Tests.Rpc;

namespace ActualLab.Fusion.Tests.MeshRpc;

public class MeshInboundDisconnectTest(ITestOutputHelper @out) : FusionTestBase(@out)
{
    [Fact(Timeout = 60_000)]
    public async Task InboundCallShouldNotHoldItsKeyAfterItsPeerIsGone()
    {
        // arrange
        var hostMap = new MeshMap();
        var clientMap = new MeshMap();
        await using var host0 = NewHost(hostMap, RpcServiceMode.Distributed);
        await using var host1 = NewHost(hostMap, RpcServiceMode.Distributed);
        await using var client = NewHost(clientMap, RpcServiceMode.Client);
        await Task.WhenAll(host0.WhenStarted, host1.WhenStarted, client.WhenStarted);
        hostMap.Reset(host1, host0); // Hosts: shard 0 is owned by host1
        clientMap.Reset(host0, host1); // The client is stale: it still sends shard 0 to host0

        // The inbound call can't run on host0 and can't be rerouted, so it waits there for the shard
        var staleCall = client.GetRequiredService<IRpcRerouteTestService>().GetValue(0, "key");
        _ = staleCall.ContinueWith(_ => { }, TaskScheduler.Default);
        await Task.Delay(2000);
        staleCall.IsCompleted.Should().BeFalse("host0 neither owns the shard nor can reroute an inbound call");

        // The same key on host0 waits for the input lock held by the inbound call
        var hostCall = host0.GetRequiredService<IRpcRerouteTestService>().GetValue(0, "key");
        await Task.Delay(1000);
        hostCall.IsCompleted.Should().BeFalse("the inbound call holds the lock of the same compute input");

        // act
        await client.DisposeAsync();

        // assert
        var result = await hostCall.WaitAsync(TimeSpan.FromSeconds(20));
        result.HostId.Should().Be(host1.Id);
    }

    [Fact(Timeout = 60_000)]
    public async Task DroppedInboundCallShouldBeResentAfterReconnect()
    {
        // arrange
        var hostMap = new MeshMap();
        var clientMap = new MeshMap();
        await using var host0 = NewHost(hostMap, RpcServiceMode.Distributed);
        await using var host1 = NewHost(hostMap, RpcServiceMode.Distributed);
        await using var client = NewHost(clientMap, RpcServiceMode.Client);
        await Task.WhenAll(host0.WhenStarted, host1.WhenStarted, client.WhenStarted);
        hostMap.Reset(host1, host0); // Hosts: shard 0 is owned by host1
        clientMap.Reset(host0, host1); // The client is stale: it still sends shard 0 to host0
        var reconnectDelayer = (ParkingReconnectDelayer)client.GetRequiredService<RpcClientPeerReconnectDelayer>();
        var staleCall = client.GetRequiredService<IRpcRerouteTestService>().GetValue(0, "key");
        await Task.Delay(2000);
        staleCall.IsCompleted.Should().BeFalse("host0 neither owns the shard nor can reroute an inbound call");
        var hostCall = host0.GetRequiredService<IRpcRerouteTestService>().GetValue(0, "key");

        // act: the client goes offline until host0 drops the inbound call (hostCall completes then),
        // and comes back once host0 owns the shard
        reconnectDelayer.ParkDelay = TimeSpan.FromMinutes(5);
        await client.GetRequiredService<RpcHub>().GetPeer(clientMap.GetShardRef(0)).Disconnect();
        var hostResult = await hostCall.WaitAsync(TimeSpan.FromSeconds(20));
        hostResult.HostId.Should().Be(host1.Id);
        staleCall.IsCompleted.Should().BeFalse("the dropped call must not be answered, only re-sent");
        hostMap.Reset(host0, host1);
        reconnectDelayer.ParkDelay = null;
        reconnectDelayer.CancelDelays();

        // assert
        var result = await staleCall.WaitAsync(TimeSpan.FromSeconds(20));
        result.HostId.Should().Be(host0.Id);
    }

    [Fact(Timeout = 60_000)]
    public async Task DroppedInboundCallWithoutAllowResendShouldFailAfterReconnect()
    {
        // arrange
        var hostMap = new MeshMap();
        var clientMap = new MeshMap();
        await using var host0 = NewHost(hostMap, RpcServiceMode.Distributed);
        await using var host1 = NewHost(hostMap, RpcServiceMode.Distributed);
        await using var client = NewHost(clientMap, RpcServiceMode.Client);
        await Task.WhenAll(host0.WhenStarted, host1.WhenStarted, client.WhenStarted);
        hostMap.Reset(host0, host1); // Hosts: shard 0 is owned by host0
        clientMap.Reset(host0, host1);
        var reconnectDelayer = (ParkingReconnectDelayer)client.GetRequiredService<RpcClientPeerReconnectDelayer>();

        // The call starts on host0 and keeps running there
        var call = client.GetRequiredService<IRpcRerouteTestService>().GetValueNoResend(0, "block");
        _ = call.ContinueWith(_ => { }, TaskScheduler.Default);
        await Task.Delay(2000);

        // act: the client goes offline, then the shard moves away from host0, so the running call is rerouted;
        // the client stays offline for several rerouting delays, so host0 notices that and drops the call
        reconnectDelayer.ParkDelay = TimeSpan.FromMinutes(5);
        await client.GetRequiredService<RpcHub>().GetPeer(clientMap.GetShardRef(0)).Disconnect();
        hostMap.Reset(host1, host0);
        await Task.Delay(8000);
        call.IsCompleted.Should().BeFalse("the client is offline, so it can't know the outcome yet");
        reconnectDelayer.ParkDelay = null;
        reconnectDelayer.CancelDelays();

        // assert: host0 reports the call as unknown, and the client can't re-send it
        var error = await call.Invoking(c => c.WaitAsync(TimeSpan.FromSeconds(20)))
            .Should().ThrowAsync<RpcException>();
        error.Which.Message.Should().Contain("AllowResend");
    }

    // Private methods

    private static MeshHost NewHost(MeshMap meshMap, RpcServiceMode serviceMode)
        => new(meshMap, serviceMode, serviceMode != RpcServiceMode.Client, (host, services) => {
            var fusion = services.AddFusion();
            fusion.AddService<IRpcRerouteTestService, RpcRerouteTestService>(host.ServiceMode);
            if (host.ServiceMode == RpcServiceMode.Client)
                services.AddSingleton<RpcClientPeerReconnectDelayer>(
                    c => new ParkingReconnectDelayer(c) { Delays = RetryDelaySeq.Fixed(0.25) });
        });
}
