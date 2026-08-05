using ActualLab.Rpc;
using ActualLab.Rpc.Compression;

namespace ActualLab.Tests.Compression;

// Compression is part of the serialization format, so both peers derive the same answer from the
// format key alone - there is nothing to negotiate, and no way for the two to disagree.
public class RpcCompressionModeTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Theory]
    [InlineData(RpcCompressionMode.None, false, false)]
    [InlineData(RpcCompressionMode.ServerToClient, true, false)]
    [InlineData(RpcCompressionMode.ClientToServer, false, true)]
    [InlineData(RpcCompressionMode.Full, true, true)]
    public void MustCompressTest(RpcCompressionMode mode, bool server, bool client)
    {
        mode.MustCompress(isServer: true).Should().Be(server);
        mode.MustCompress(isServer: false).Should().Be(client);
    }

    // The two directions a peer sees are mirror images of its peer's
    [Theory]
    [InlineData("msgpack6c", false, false)]
    [InlineData("msgpack6c-lz4", false, true)]
    [InlineData("msgpack6-lz4", false, true)]
    [InlineData("msgpack6c-lz4f", true, true)]
    [InlineData("mempack6c-lz4f", true, true)]
    public void FormatDefinesBothDirectionsTest(string key, bool clientCompresses, bool serverCompresses)
    {
        var format = RpcSerializationFormat.All.Single(x => x.Key == key);
        var mode = format.CompressionMode;
        mode.MustCompress(isServer: false).Should().Be(clientCompresses);
        mode.MustCompress(isServer: true).Should().Be(serverCompresses);
        (format.CompressionFormat is not null).Should().Be(clientCompresses || serverCompresses);
    }

    // A mode without a codec - or a codec without a mode - is no compression at all
    [Fact]
    public void HalfConfiguredFormatIsUncompressedTest()
    {
        var noFormat = new RpcSerializationFormat("test-no-format",
            () => RpcSerializationFormat.MessagePackV6C.ArgumentSerializer,
            RpcSerializationFormat.MessagePackV6C.MessageSerializerFactory,
            compressionMode: RpcCompressionMode.Full);
        noFormat.CompressionFormat.Should().BeNull();
        noFormat.CompressionMode.Should().Be(RpcCompressionMode.None);

        var noMode = new RpcSerializationFormat("test-no-mode",
            () => RpcSerializationFormat.MessagePackV6C.ArgumentSerializer,
            RpcSerializationFormat.MessagePackV6C.MessageSerializerFactory,
            RpcCompressionFormat.LZ4);
        noMode.CompressionFormat.Should().BeNull();
        noMode.CompressionMode.Should().Be(RpcCompressionMode.None);
    }
}
