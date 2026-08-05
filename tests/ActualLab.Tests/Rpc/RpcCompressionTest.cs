using ActualLab.Rpc;

namespace ActualLab.Tests.Rpc;

// Frame compression over a real WebSocket transport. The reconnection cases matter most: a codec
// pair is per-connection state, so a reconnect must start both halves over.
public class RpcCompressionTest : RpcTestBase
{
    public RpcCompressionTest(ITestOutputHelper @out) : base(@out)
        => ExposeBackend = true;

    protected override void ConfigureServices(IServiceCollection services, bool isClient)
    {
        base.ConfigureServices(services, isClient);
        var rpc = services.AddRpc();
        var commander = services.AddCommander();
        if (isClient) {
            rpc.AddClient<ITestRpcService>();
            commander.AddService<ITestRpcService>();
        }
        else {
            rpc.AddServer<ITestRpcService, TestRpcService>();
            commander.AddService<TestRpcService>();
        }
    }

    // "-lz4" compresses server -> client only: a client's outbound traffic is mostly small call
    // headers, where compression costs more than it saves.
    [Fact]
    public async Task ServerToClientTest()
    {
        await UseFormat("msgpack6c-lz4");
        await using var _ = await WebHost.Serve();
        var client = ClientServices.RpcHub().GetClient<ITestRpcService>();
        var peer = ClientServices.RpcHub().GetClientPeer(ClientPeerRef);

        (await client.Div(6, 2)).Should().Be(3);
        peer.InboundCompression!.Id.Value.Should().Be("lz4");
        peer.OutboundCompression.Should().BeNull();

        for (var i = 0; i < 200; i++)
            (await client.Add(i, 1)).Should().Be(i + 1);

        (await client.GetBytes(100_000)).Length.Should().Be(100_000);
        await Assert.ThrowsAsync<DivideByZeroException>(() => client.Div(1, 0));
    }

    // "-lz4f" compresses both directions
    [Fact]
    public async Task BothDirectionsTest()
    {
        await UseFormat("msgpack6c-lz4f");
        await using var _ = await WebHost.Serve();
        var client = ClientServices.RpcHub().GetClient<ITestRpcService>();
        var peer = ClientServices.RpcHub().GetClientPeer(ClientPeerRef);

        (await client.Div(6, 2)).Should().Be(3);
        peer.InboundCompression!.Id.Value.Should().Be("lz4");
        peer.OutboundCompression!.Id.Value.Should().Be("lz4");

        for (var i = 0; i < 200; i++)
            (await client.Add(i, 1)).Should().Be(i + 1);
        (await client.GetBytes(100_000)).Length.Should().Be(100_000);
    }

    // A reconnect must reset both halves of the pair on both peers - a decoder still holding the
    // old connection's dictionary would decode the first frame of the new one into garbage.
    [Theory]
    [InlineData("msgpack6c-lz4")]
    [InlineData("msgpack6c-lz4f")]
    public async Task ReconnectTest(string format)
    {
        await UseFormat(format);
        await using var _ = await WebHost.Serve();
        var client = ClientServices.RpcHub().GetClient<ITestRpcService>();
        var peer = ClientServices.RpcHub().GetClientPeer(ClientPeerRef);
        var compressBothWays = format.EndsWith("f", StringComparison.Ordinal);

        for (var i = 0; i < 5; i++) {
            // Enough traffic to build a dictionary worth losing on reconnect
            for (var j = 0; j < 50; j++)
                (await client.Add(j, i)).Should().Be(j + i);

            await peer.Disconnect().ConfigureAwait(false);
            (await client.Div(6, 2)).Should().Be(3);
            peer.InboundCompression!.Id.Value.Should().Be("lz4");
            (peer.OutboundCompression is not null).Should().Be(compressBothWays);
        }
    }

    [Fact]
    public async Task ReconnectMidStreamTest()
    {
        await UseFormat("msgpack6c-lz4f");
        await using var _ = await WebHost.Serve();
        var client = ClientServices.RpcHub().GetClient<ITestRpcService>();
        var peer = ClientServices.RpcHub().GetClientPeer(ClientPeerRef);

        var stream = await client.StreamInt32(100, -1, new RandomTimeSpan(0.01, 1));
        var countTask = stream.CountAsync();
        await Delay(0.1);
        await peer.Disconnect().ConfigureAwait(false);

        (await countTask.AsTask().WaitAsync(TimeSpan.FromSeconds(20))).Should().Be(100);
    }

    // The uncompressed variant of the same format must stay exactly what it was
    [Fact]
    public async Task PlainFormatIsNeverCompressedTest()
    {
        await UseFormat("msgpack6c");
        await using var _ = await WebHost.Serve();
        var client = ClientServices.RpcHub().GetClient<ITestRpcService>();
        var peer = ClientServices.RpcHub().GetClientPeer(ClientPeerRef);

        (await client.Div(6, 2)).Should().Be(3);
        peer.InboundCompression.Should().BeNull();
        peer.OutboundCompression.Should().BeNull();
    }

    // Private methods

    private async Task UseFormat(string format)
    {
        SerializationFormat = format;
        await Reset();
    }
}
