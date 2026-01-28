using ActualLab.Rpc;
using ActualLab.Rpc.Clients;
using ActualLab.Rpc.WebSockets;
using ActualLab.Testing.Collections;

namespace ActualLab.Tests.Rpc;

[Collection(nameof(TimeSensitiveTests)), Trait("Category", nameof(TimeSensitiveTests))]
public class RpcWebSocketTransportFrameDelayTest : RpcTestBase
{
    private readonly TaskCompletionSource<Unit> _delayCts = TaskCompletionSourceExt.New<Unit>();

    public RpcWebSocketTransportFrameDelayTest(ITestOutputHelper @out) : base(@out)
        => ExposeBackend = true;

    protected override void ConfigureServices(IServiceCollection services, bool isClient)
    {
        base.ConfigureServices(services, isClient);
        var rpc = services.AddRpc();
        var commander = services.AddCommander();
        if (isClient) {
            rpc.AddClient<ITestRpcServiceClient>(nameof(ITestRpcService));
            commander.AddService<ITestRpcServiceClient>();
            rpc.AddClient<ITestRpcBackend>();
            commander.AddService<ITestRpcBackend>();
        }
        else {
            rpc.AddServer<ITestRpcService, TestRpcService>();
            commander.AddService<TestRpcService>();
            rpc.AddServer<ITestRpcBackend, TestRpcBackend>();
            commander.AddService<TestRpcBackend>();
        }
    }

    protected override void ConfigureTestServices(IServiceCollection services, bool isClient)
    {
        base.ConfigureTestServices(services, isClient);
        if (!isClient)
            return;

        services.AddSingleton<RpcWebSocketClientOptions>(_ => new RpcWebSocketClientOptions() {
            HostUrlResolver = _ => WebHost.ServerUri.ToString(),
            FrameDelayerFactory = null,
            WebSocketTransportOptionsFactory = (_, _) => RpcWebSocketTransport.Options.Default with {
                // Make frames effectively “never auto-flush by size”, so flushing is triggered
                // solely by the scheduled (delayed) flush.
                FrameSize = 1_000_000,
                FrameDelayerFactory = () => _ => _delayCts.Task,
            },
        });
    }

    [Fact]
    public async Task DelayedFlushBlocksSendingUntilDelayCompletes()
    {
        await ResetClientServices();
        await using var _ = await WebHost.Serve();
        var services = ClientServices;
        var peer = services.RpcHub().GetClientPeer(ClientPeerRef);
        var client = services.RpcHub().GetClient<ITestRpcServiceClient>();

        var callTask = client.Div(1, 1);
        await Task.Delay(100);
        callTask.IsCompleted.Should().BeFalse();

        _delayCts.TrySetResult(default);

        (await callTask.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false)).Should().Be(1);
        await AssertNoCalls(peer, Out);
    }
}
