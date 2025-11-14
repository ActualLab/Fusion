using ActualLab.Fusion.Extensions;
using ActualLab.Fusion.Tests.Services;
using ActualLab.Rpc;
using ActualLab.Rpc.Testing;
using ActualLab.Testing.Collections;

namespace ActualLab.Fusion.Tests.Rpc;

[Collection(nameof(TimeSensitiveTests)), Trait("Category", nameof(TimeSensitiveTests))]
public class FusionRpcBasicTest(ITestOutputHelper @out) : SimpleFusionTestBase(@out)
{
    protected override void ConfigureServices(ServiceCollection services)
    {
        base.ConfigureServices(services);
        var fusion = services.AddFusion();
        fusion.AddService<ICounterService, CounterService>(RpcServiceMode.Distributed);
    }

    [Fact]
    public async Task DistributedTest()
    {
        var services = CreateServices();
        var testClient = services.GetRequiredService<RpcTestClient>();
        var clientPeer = testClient.GetConnection(x => !x.IsBackend).ClientPeer;
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
    public async Task PeerMonitorTest()
    {
        var services = CreateServices();
        var testClient = services.GetRequiredService<RpcTestClient>();
        var clientPeer = testClient.GetConnection(x => !x.IsBackend).ClientPeer;
        var monitor = new RpcPeerStateMonitor(services, clientPeer.Ref);
        var state = monitor.State;
        await state.Computed.When(x => x.Kind == RpcPeerStateKind.JustConnected).WaitAsync(TimeSpan.FromSeconds(1));
        await state.Computed.When(x => x.Kind == RpcPeerStateKind.Connected).WaitAsync(TimeSpan.FromSeconds(2));

        _ = clientPeer.Disconnect(new InvalidOperationException("Disconnected!"));
        await state.Computed.When(x => x.Kind == RpcPeerStateKind.JustDisconnected).WaitAsync(TimeSpan.FromSeconds(1));
        await state.Computed.When(x => x.Kind == RpcPeerStateKind.Disconnected).WaitAsync(TimeSpan.FromSeconds(5));

        await testClient[clientPeer.Ref].Connect();
        await state.Computed.When(x => x.Kind == RpcPeerStateKind.JustConnected).WaitAsync(TimeSpan.FromSeconds(1));
        await state.Computed.When(x => x.Kind == RpcPeerStateKind.Connected).WaitAsync(TimeSpan.FromSeconds(2));
    }
}
