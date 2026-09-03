using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Testing;

namespace ActualLab.Tests.Rpc;

public class RpcHandshakeVersionSetLimitTest(ITestOutputHelper @out) : RpcLocalTestBase(@out)
{
    protected override void ConfigureServices(ServiceCollection services)
    {
        base.ConfigureServices(services);
        var commander = services.AddCommander();
        commander.AddService<TestRpcService>();
        commander.AddService<TestRpcBackend>();

        var rpc = services.AddRpc();
        rpc.AddServerAndClient<ITestRpcService, TestRpcService>();
        rpc.AddServerAndClient<ITestRpcBackend, TestRpcBackend>();
        services.AddSingleton<RpcPeerOptions>(_ => RpcPeerOptions.Default with {
            UseRandomHandshakeIndex = true,
            PeerFactory = (hub, route) => route.Ref.IsServer
                ? new RpcServerPeer(hub, route)
                : new RpcClientPeer(hub, route, route.Ref.IsBackend ? null : NewVersionSet(64)),
        });
    }

    [Fact]
    public void LimitTest()
    {
        RpcHandshake.ValidateApiVersionSet(VersionSet.Empty);
        RpcHandshake.ValidateApiVersionSet(RpcDefaults.ApiPeerVersions);
        RpcHandshake.ValidateApiVersionSet(RpcDefaults.BackendPeerVersions);
        RpcHandshake.ValidateApiVersionSet(NewVersionSet(RpcHandshake.MaxApiVersionSetCount));

        Assert.Throws<RpcException>(() => RpcHandshake.ValidateApiVersionSet(
            NewVersionSet(RpcHandshake.MaxApiVersionSetCount + 1)));
        Assert.Throws<RpcException>(() => RpcHandshake.ValidateApiVersionSet(
            new VersionSet(new string('x', RpcHandshake.MaxApiVersionSetLength), "1.0")));
    }

    [Fact]
    public async Task RejectionIsPerPeerTest()
    {
        await using var services = CreateServices();
        var testClient = services.GetRequiredService<RpcTestClient>();
        var apiConnection = testClient.GetConnection(x => !x.IsBackend);
        var backendConnection = testClient.GetConnection(x => x.IsBackend);

        await backendConnection.ServerPeer.WhenConnected(TimeSpan.FromSeconds(10));
        var backendClient = services.GetRequiredService<ITestRpcBackend>();
        var value = new Tuple<int>(1);
        (await backendClient.Polymorph(value)).Should().Be(value);

        var whenConnectedResult = await apiConnection.ServerPeer
            .WhenConnected(TimeSpan.FromSeconds(1))
            .ResultAwait();
        whenConnectedResult.Error.Should().BeOfType<RpcTimeoutException>()
            .Which.TimeoutKind.Should().Be(RpcTimeoutKind.Connect);
    }

    // Private methods

    private static VersionSet NewVersionSet(int count)
        => new(Enumerable.Range(0, count)
            .Select(i => ($"Scope{i}", new Version(1, i)))
            .ToArray());
}
