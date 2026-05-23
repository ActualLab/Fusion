using ActualLab.Rpc;
using ActualLab.Rpc.Clients;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Testing;
using ActualLab.Testing.Collections;

namespace ActualLab.Tests.Rpc;

[Collection(nameof(TimeSensitiveTests)), Trait("Category", nameof(TimeSensitiveTests))]
public class RpcAlternatingClientTest(ITestOutputHelper @out) : RpcLocalTestBase(@out)
{
    protected override void ConfigureServices(ServiceCollection services)
    {
        base.ConfigureServices(services);
        var commander = services.AddCommander();
        var rpc = services.AddRpc();

        rpc.AddServerAndClient<ITestRpcService, TestRpcService>();
        commander.AddHandlers<TestRpcService>();

        services.AddSingleton<PrimaryRpcTestClient>();
        services.AddSingleton<SecondaryRpcTestClient>();
        services.RemoveAll(d => d.ServiceType == typeof(RpcClient));
        services.AddSingleton(c => new TrackingRpcAlternatingClient(
            c,
            c.GetRequiredService<PrimaryRpcTestClient>(),
            c.GetRequiredService<SecondaryRpcTestClient>()));
        services.AddAlias<RpcAlternatingClient, TrackingRpcAlternatingClient>();
        services.AddAlias<RpcClient, TrackingRpcAlternatingClient>();
    }

    protected override void StartServices(IServiceProvider services)
    {
        var client = services.GetRequiredService<PrimaryRpcTestClient>();
        _ = client.CreateDefaultConnection().Connect();
    }

    [Fact]
    public async Task AlternatesOnManualReconnect()
    {
        await using var services = CreateServices();
        var client = services.RpcHub().GetClient<ITestRpcService>();
        var primaryClient = services.GetRequiredService<PrimaryRpcTestClient>();
        var secondaryClient = services.GetRequiredService<SecondaryRpcTestClient>();
        var alternatingClient = services.GetRequiredService<TrackingRpcAlternatingClient>();
        var primaryConnection = primaryClient.CreateDefaultConnection();
        var secondaryConnection = secondaryClient.CreateDefaultConnection();
        var peer = primaryConnection.ClientPeer;

        (await client.Add(1, 1).ConfigureAwait(false)).Should().Be(2);
        await peer.WhenConnected(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
        alternatingClient.HasMarkDisconnectedBeforeMarkConnected.Should().BeFalse();
        GetAlternatingState(peer).LastClient.Should().BeSameAs(primaryClient);

        await ReconnectAndAssert(
            primaryConnection, secondaryConnection, client, secondaryClient).ConfigureAwait(false);
        await ReconnectAndAssert(
            secondaryConnection, primaryConnection, client, primaryClient).ConfigureAwait(false);
    }

    // Private methods

    private static async Task ReconnectAndAssert(
        RpcTestConnection disconnectConnection,
        RpcTestConnection connectConnection,
        ITestRpcService client,
        RpcClient expectedClient)
    {
        await disconnectConnection.Disconnect().ConfigureAwait(false);
        await connectConnection.Connect()
            .WaitAsync(TimeSpan.FromSeconds(10))
            .ConfigureAwait(false);
        (await client.Add(1, 1).ConfigureAwait(false)).Should().Be(2);
        GetAlternatingState(connectConnection.ClientPeer).LastClient.Should().BeSameAs(expectedClient);
    }

    private static RpcAlternatingClient.State GetAlternatingState(RpcClientPeer peer)
        => peer.Extensions.KeylessGet<RpcAlternatingClient.State>()
            ?? throw new InvalidOperationException("RpcAlternatingClient state is missing.");

    // Nested types

    private sealed class TrackingRpcAlternatingClient(IServiceProvider services, params RpcClient[] clients)
        : RpcAlternatingClient(services, clients)
    {
        public bool HasMarkDisconnectedBeforeMarkConnected { get; private set; }
        public int MarkConnectedCount { get; private set; }

        public override void OnConnectionStateChange(RpcClientPeer clientPeer, RpcPeerConnectionState connectionState)
        {
            base.OnConnectionStateChange(clientPeer, connectionState);
            if (connectionState.IsConnected())
                MarkConnectedCount++;
            else if (connectionState.IsDisconnected()) {
                if (MarkConnectedCount == 0)
                    HasMarkDisconnectedBeforeMarkConnected = true;
            }
        }
    }

    private sealed class PrimaryRpcTestClient(IServiceProvider services) : RpcTestClient(services);

    private sealed class SecondaryRpcTestClient(IServiceProvider services) : RpcTestClient(services);
}
