using ActualLab.Rpc;
using ActualLab.Rpc.Testing;
using ActualLab.Testing.Collections;

namespace ActualLab.Tests.Rpc;

[Collection(nameof(TimeSensitiveTests)), Trait("Category", nameof(TimeSensitiveTests))]
public class RpcReconnectionLimitTest(ITestOutputHelper @out) : RpcLocalTestBase(@out)
{
    protected override void ConfigureServices(ServiceCollection services)
    {
        base.ConfigureServices(services);
        services.AddRpc().AddServerAndClient<ITestRpcService, TestRpcService>();
    }

    [Fact]
    public async Task ReconnectionLimitTest()
    {
        await using var services = CreateServices(services => {
            services.AddSingleton(new RpcLimits(false) {
                ConnectTimeout = TimeSpan.FromSeconds(0.5),
                MaxReconnectCount = 3,
                MaxReconnectDuration = TimeSpan.FromSeconds(30),
            });
        });
        var rpcHub = services.RpcHub();
        var clientPeer = (RpcClientPeer)rpcHub.GetPeer(RpcPeerRef.Default);

        // Disconnect and keep it disconnected
        var testClient = services.GetRequiredService<RpcTestClient>();
        var connection = testClient.GetConnection(RpcPeerRef.Default);
        await connection.Disconnect();

        // Wait for it to fail enough times
        await Task.Delay(5000);

        // It should eventually reach the limit and stop
        var startAt = DateTime.UtcNow;
        while (DateTime.UtcNow - startAt < TimeSpan.FromSeconds(30)) {
            if (clientPeer.ConnectionState.IsFinal)
                break;
            await Task.Delay(500);
        }

        clientPeer.ConnectionState.IsFinal.Should().BeTrue();
        clientPeer.ConnectionState.Value.Error.Should().BeOfType<RpcReconnectFailedException>();

        // Verify that it gets removed from the Hub eventually (after Hub.PeerRemoveDelay)
        // For testing we might want to decrease PeerRemoveDelay
    }

    [Fact]
    public async Task ReconnectionDurationLimitTest()
    {
        await using var services = CreateServices(services => {
            services.AddSingleton(new RpcLimits(false) {
                ConnectTimeout = TimeSpan.FromSeconds(0.5),
                MaxReconnectCount = 1000,
                MaxReconnectDuration = TimeSpan.FromSeconds(2),
            });
        });
        var rpcHub = services.RpcHub();
        var clientPeer = (RpcClientPeer)rpcHub.GetPeer(RpcPeerRef.Default);

        // Disconnect and keep it disconnected
        var testClient = services.GetRequiredService<RpcTestClient>();
        var connection = testClient.GetConnection(RpcPeerRef.Default);
        await connection.Disconnect();

        // It should eventually reach the duration limit and stop
        var startAt = DateTime.UtcNow;
        while (DateTime.UtcNow - startAt < TimeSpan.FromSeconds(30)) {
            if (clientPeer.ConnectionState.IsFinal)
                break;
            await Task.Delay(500);
        }

        clientPeer.ConnectionState.IsFinal.Should().BeTrue();
        clientPeer.ConnectionState.Value.Error.Should().BeOfType<RpcReconnectFailedException>();
    }
}
