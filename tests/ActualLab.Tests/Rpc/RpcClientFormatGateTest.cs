using ActualLab.Rpc;

namespace ActualLab.Tests.Rpc;

public class RpcClientFormatGateTest(ITestOutputHelper @out) : RpcTestBase(@out)
{
    protected override void ConfigureServices(IServiceCollection services, bool isClient)
    {
        base.ConfigureServices(services, isClient);
        var rpc = services.AddRpc();
        var commander = services.AddCommander();
        if (isClient) {
            rpc.AddClient<ITestRpcServiceClient>(nameof(ITestRpcService));
            commander.AddService<ITestRpcServiceClient>();
        }
        else {
            rpc.AddServer<ITestRpcService, TestRpcService>();
            commander.AddService<TestRpcService>();
        }
    }

    [Fact]
    public async Task NewtonsoftFormatIsRejectedByDefault()
    {
        ClientDeniedFormatKeys = RpcSerializationFormatResolver.DefaultClientDeniedFormatKeys;
        SerializationFormat = "njson5";
        await using var _ = await WebHost.Serve();

        await AssertFormatRejected(ClientServices.RpcHub().GetClientPeer(ClientPeerRef));
    }

    [Fact]
    public async Task NewtonsoftFormatConnectsWhenAllowed()
    {
        ClientDeniedFormatKeys = ImmutableHashSet<string>.Empty;
        SerializationFormat = "njson5";
        await using var _ = await WebHost.Serve();

        var client = ClientServices.RpcHub().GetClient<ITestRpcServiceClient>();
        (await client.Div(6, 2)).Should().Be(3);
    }

    [Fact]
    public async Task AllowedFormatIsUnaffected()
    {
        ClientDeniedFormatKeys = RpcSerializationFormatResolver.DefaultClientDeniedFormatKeys;
        SerializationFormat = "json5";
        await using var _ = await WebHost.Serve();

        var client = ClientServices.RpcHub().GetClient<ITestRpcServiceClient>();
        (await client.Div(6, 2)).Should().Be(3);
    }

    [Fact]
    public async Task UnregisteredFormatIsStillRejected()
    {
        ClientDeniedFormatKeys = RpcSerializationFormatResolver.DefaultClientDeniedFormatKeys;
        await using var _ = await WebHost.Serve();

        var bogusFormat = new RpcSerializationFormat("bogus-format",
            () => RpcSerializationFormat.SystemJsonV5.ArgumentSerializer,
            RpcSerializationFormat.SystemJsonV5.MessageSerializerFactory);
        var clientServices = new ServiceCollection();
        clientServices.AddSingleton<TestServiceProviderTag>();
        ConfigureServices(clientServices, isClient: true);
        clientServices.AddSingleton(_ => new RpcSerializationFormatResolver("bogus-format", [bogusFormat]));
        await using var sp = clientServices.BuildServiceProvider();

        await AssertFormatRejected(sp.RpcHub().GetClientPeer(ClientPeerRef));
    }

    // Private methods

    private static async Task AssertFormatRejected(RpcPeer peer)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var connectionState = peer.ConnectionState;
        try {
            while (!connectionState.IsFinal)
                connectionState = await connectionState.WhenNext(cts.Token).ConfigureAwait(false);
        }
        catch (RpcSerializationFormatException) {
            // Expected - SetFinal(error) makes WhenNext throw
        }
        peer.ConnectionState.IsFinal.Should().BeTrue();
        peer.ConnectionState.Value.Error.Should().BeOfType<RpcSerializationFormatException>();
    }
}
