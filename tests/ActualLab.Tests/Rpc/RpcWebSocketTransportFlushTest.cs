using ActualLab.OS;
using ActualLab.Rpc;
using ActualLab.Rpc.Clients;
using ActualLab.Rpc.WebSockets;
using ActualLab.Testing.Collections;

namespace ActualLab.Tests.Rpc;

[Collection(nameof(TimeSensitiveTests)), Trait("Category", nameof(TimeSensitiveTests))]
public class RpcWebSocketTransportFlushTest : RpcTestBase
{
    public RpcWebSocketTransportFlushTest(ITestOutputHelper @out) : base(@out)
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
        services.AddSingleton<RpcWebSocketClientOptions>(_ => new RpcWebSocketClientOptions() {
            HostUrlResolver = _ => WebHost.ServerUri.ToString(),
            FrameDelayerFactory = null,
            WebSocketTransportOptionsFactory = (_, _) => RpcWebSocketTransport.Options.Default with {
                // Make frames effectively “never auto-flush by size”, so the sender must
                // reliably pick up and send small buffered data when it goes idle.
                FrameSize = 1_000_000,
            },
        });
    }

    [Fact]
    public async Task SmallBufferMustFlushWithoutExtraWrite()
    {
        RpcFrameDelayerFactory = null;
        await ResetClientServices();
        await using var _ = await WebHost.Serve();
        var services = ClientServices;
        var peer = services.RpcHub().GetClientPeer(ClientPeerRef);
        var client = services.RpcHub().GetClient<ITestRpcServiceClient>();

        // A few concurrent calls with per-call timeout to catch “buffer stuck until next write”.
        var threadCount = Math.Max(2, HardwareInfo.ProcessorCount / 2);
        var tasks = new Task[threadCount];
        for (var t = 0; t < threadCount; t++) {
            tasks[t] = Task.Run(async () => {
                for (var i = 0; i < 200; i++)
                    (await client.Div(i, 1).WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false))
                        .Should().Be(i);
            }, CancellationToken.None);
        }

        await Task.WhenAll(tasks);
        await AssertNoCalls(peer, Out);
    }
}
