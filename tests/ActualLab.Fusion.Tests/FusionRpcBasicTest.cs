using ActualLab.Fusion.Extensions;
using ActualLab.Fusion.Tests.Services;
using ActualLab.Rpc;
using ActualLab.Rpc.Testing;
using ActualLab.Testing.Collections;

namespace ActualLab.Fusion.Tests;

[Collection(nameof(TimeSensitiveTests)), Trait("Category", nameof(TimeSensitiveTests))]
public class FusionRpcBasicTest(ITestOutputHelper @out) : SimpleFusionTestBase(@out)
{
    protected RpcServiceMode ServiceMode { get; set; } = RpcServiceMode.DistributedPair;

    protected override void ConfigureServices(ServiceCollection services)
    {
        base.ConfigureServices(services);
        var fusion = services.AddFusion();
        fusion.AddService<ICounterService, CounterService>(ServiceMode);
    }

    [Fact]
    public async Task DistributedTest()
    {
        ServiceMode = RpcServiceMode.Distributed;
        var services = CreateServices();
        var testClient = services.GetRequiredService<RpcTestClient>();
        var clientPeer = testClient.Connections.First().Value.ClientPeer;
        await clientPeer.WhenConnected();

        var counters = services.GetRequiredService<ICounterService>();

        var c = Computed.GetExisting(() => counters.Get("a"));
        c.Should().BeNull();

        await Assert.ThrowsAnyAsync<InvalidOperationException>(async () => {
            await counters.Get("a");
        });

        await Assert.ThrowsAnyAsync<InvalidOperationException>(async () => {
            await Computed.Capture(() => counters.Get("a"));
        });
    }

    [Fact]
    public async Task DistributedPairTest()
    {
        var services = CreateServices();
        var counters = services.GetRequiredService<ICounterService>();

        var c = Computed.GetExisting(() => counters.Get("a"));
        c.Should().BeNull();

        c = await Computed.Capture(() => counters.Get("a"));
        c.Value.Should().Be(0);
        var c1 = Computed.GetExisting(() => counters.Get("a"));
        c1.Should().BeSameAs(c);

        await counters.Increment("a");
        await TestExt.When(
            () => c.IsConsistent().Should().BeFalse(),
            TimeSpan.FromSeconds(1));

        c1 = Computed.GetExisting(() => counters.Get("a"));
        c1?.IsConsistent().Should().BeFalse();
    }

    [Fact]
    public async Task PeerMonitorTest()
    {
        var services = CreateServices();
        var testClient = services.GetRequiredService<RpcTestClient>();
        var clientPeer = testClient.Connections.First().Value.ClientPeer;
        var monitor = new RpcPeerStateMonitor(services, clientPeer.Ref);
        var state = monitor.State;
        await state.When(x => x.Kind == RpcPeerStateKind.JustConnected).WaitAsync(TimeSpan.FromSeconds(1));
        await state.When(x => x.Kind == RpcPeerStateKind.Connected).WaitAsync(TimeSpan.FromSeconds(2));

        _ = clientPeer.Disconnect(false, new InvalidOperationException("Disconnected!"));
        await state.When(x => x.Kind == RpcPeerStateKind.JustDisconnected).WaitAsync(TimeSpan.FromSeconds(1));
        await state.When(x => x.Kind == RpcPeerStateKind.Disconnected).WaitAsync(TimeSpan.FromSeconds(5));

        await testClient[clientPeer.Ref].Connect();
        await state.When(x => x.Kind == RpcPeerStateKind.JustConnected).WaitAsync(TimeSpan.FromSeconds(1));
        await state.When(x => x.Kind == RpcPeerStateKind.Connected).WaitAsync(TimeSpan.FromSeconds(2));
    }
}
