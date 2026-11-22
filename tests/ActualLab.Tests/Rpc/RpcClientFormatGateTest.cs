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
        BackendOnlyFormatKeys = RpcDefaultWireFormats.BackendOnlyFormatKeys;
        WireFormat = "njson5";
        await using var _ = await WebHost.Serve();

        await AssertFormatRejected(ClientServices.RpcHub().GetClientPeer(ClientPeerRef));
    }

    [Fact]
    public async Task NewtonsoftFormatConnectsWhenAllowed()
    {
        BackendOnlyFormatKeys = ImmutableHashSet<string>.Empty;
        WireFormat = "njson5";
        await using var _ = await WebHost.Serve();

        var client = ClientServices.RpcHub().GetClient<ITestRpcServiceClient>();
        (await client.Div(6, 2)).Should().Be(3);
    }

    [Fact]
    public async Task AllowedFormatIsUnaffected()
    {
        BackendOnlyFormatKeys = RpcDefaultWireFormats.BackendOnlyFormatKeys;
        WireFormat = "json5";
        await using var _ = await WebHost.Serve();

        var client = ClientServices.RpcHub().GetClient<ITestRpcServiceClient>();
        (await client.Div(6, 2)).Should().Be(3);
    }

    [Fact]
    public async Task UnregisteredFormatIsStillRejected()
    {
        BackendOnlyFormatKeys = RpcDefaultWireFormats.BackendOnlyFormatKeys;
        await using var _ = await WebHost.Serve();

        var bogusFormat = new RpcWireFormat("bogus-format",
            () => RpcDefaultWireFormats.SystemJsonV5.ArgumentListSerializer,
            RpcDefaultWireFormats.SystemJsonV5.MessageSerializerFactory,
            DataFormat.Text);
        var clientServices = new ServiceCollection();
        clientServices.AddSingleton<TestServiceProviderTag>();
        ConfigureServices(clientServices, isClient: true);
        var clientFormats = new RpcWireFormatConfiguration();
        clientFormats.Clear();
        clientFormats.Add(bogusFormat);
        clientFormats.DefaultFormat = bogusFormat;
        clientServices.AddSingleton(new RpcWireFormatResolver(clientFormats));
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
        catch (RpcWireFormatException) {
            // Expected - SetFinal(error) makes WhenNext throw
        }
        peer.ConnectionState.IsFinal.Should().BeTrue();
        peer.ConnectionState.Value.Error.Should().BeOfType<RpcWireFormatException>();
    }
}
