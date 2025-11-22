using ActualLab.Rpc;
using ActualLab.Testing.Collections;

namespace ActualLab.Tests.Rpc;

[Collection(nameof(TimeSensitiveTests)), Trait("Category", nameof(TimeSensitiveTests))]
public class RpcWebSocketLegacyNameTest : RpcTestBase
{
    public VersionSet ClientPeerVersions = RpcDefaults.ApiPeerVersions;

    public RpcWebSocketLegacyNameTest(ITestOutputHelper @out) : base(@out)
        => ExposeBackend = true;

    protected override void ConfigureServices(IServiceCollection services, bool isClient)
    {
        base.ConfigureServices(services, isClient);
        var rpc = services.AddRpc();
        var commander = services.AddCommander();
        if (isClient) {
            rpc.AddClient<ITestRpcService>();
            commander.AddService<ITestRpcService>();
            services.AddSingleton<RpcPeerOptions>(_ => RpcPeerOptions.Default with {
                PeerFactory = (hub, peerRef) => new RpcClientPeer(hub, peerRef, ClientPeerVersions),
            });
        }
        else {
            rpc.AddServer<ITestRpcService, TestRpcService>();
            rpc.AddServer<ITestRpcLegacyService, TestRpcLegacyService>();
            commander.AddService<TestRpcService>();
        }
    }

    [Fact]
    public async Task Test_v0_1()
    {
        ClientPeerVersions = new(RpcDefaults.ApiScope, "v0.1");
        await using var _ = await WebHost.Serve();

        var services = ClientServices;
        var client = services.GetRequiredService<ITestRpcService>();
        (await client.Add(1, 1)).Should().Be(2);

        var version = await client.GetVersion();
        var peer = WebHost.Services.RpcHub().InternalServices.Peers.Values
            .First(x => x.ServerMethodResolver.NextResolver != null);
        var serverMethodResolver = peer.ServerMethodResolver;
        Out.WriteLine(serverMethodResolver.ToString());

        serverMethodResolver.MethodByRef!.Count.Should().Be(1);
        version.Should().Be("0.1");
    }

    [Fact]
    public async Task Test_v0_5()
    {
        ClientPeerVersions = new(RpcDefaults.ApiScope, "0.5");
        await using var _ = await WebHost.Serve();
        var services = ClientServices;
        var client = services.GetRequiredService<ITestRpcService>();
        (await client.Add(1, 1)).Should().Be(2);

        var version = await client.GetVersion();
        var peer = WebHost.Services.RpcHub().InternalServices.Peers.Values
            .First(x => x.ServerMethodResolver.NextResolver != null);
        var serverMethodResolver = peer.ServerMethodResolver;
        Out.WriteLine(serverMethodResolver.ToString());

        serverMethodResolver.MethodByRef!.Count.Should().Be(1);
        version.Should().Be("0.5");
    }

    [Fact]
    public async Task Test_v1_0()
    {
        await using var _ = await WebHost.Serve();
        var services = ClientServices;
        var client = services.GetRequiredService<ITestRpcService>();
        (await client.Add(1, 1)).Should().Be(2);

        var version = await client.GetVersion();
        var peer = WebHost.Services.RpcHub().InternalServices.Peers.Values
            .First(x => x.ServerMethodResolver.NextResolver != null);
        var serverMethodResolver = peer.ServerMethodResolver;
        Out.WriteLine(serverMethodResolver.ToString());

        serverMethodResolver.MethodByRef!.Count.Should().Be(2);
        version.Should().Be("1.0*");
    }
}
