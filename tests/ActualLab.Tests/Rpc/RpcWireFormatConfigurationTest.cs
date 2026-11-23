using ActualLab.Rpc;
using ActualLab.Rpc.Compression;

namespace ActualLab.Tests.Rpc;

public class RpcWireFormatConfigurationTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public void AddLZ4VariantsTest()
    {
        var f = new RpcWireFormatConfiguration();
        f.AddLZ4Variants("lz4");
        f.ContainsKey("msgpack6c-lz4").Should().BeTrue();
        f.ContainsKey("msgpack6c-lz4f").Should().BeFalse();
        f.ContainsKey("json5-lz4").Should().BeFalse();
        f.ContainsKey("msgpack6c-lz4-lz4").Should().BeFalse();

        f.AddLZ4Variants("lz4f", compressionMode: RpcCompressionMode.Full, filter: x => x.Key == "msgpack6");
        f.ContainsKey("msgpack6-lz4f").Should().BeTrue();
        f.ContainsKey("msgpack6c-lz4f").Should().BeFalse();
    }

    [Fact]
    public void RepeatedAddLZ4VariantsShouldFailTest()
    {
        var f = new RpcWireFormatConfiguration();
        f.AddLZ4Variants("lz4");
        f.AddLZ4Variants("lz4f", compressionMode: RpcCompressionMode.Full);
        f.DefaultFormat = f["msgpack6c-lz4f"];
        var formats = f.ToDictionary(x => x.Key, x => x.Value);
        Assert.Throws<InvalidOperationException>(
            () => f.AddLZ4Variants("lz4f", compressionMode: RpcCompressionMode.Full));
        Assert.Throws<InvalidOperationException>(
            () => f.AddLZ4Variants("lz4f", compressionMode: RpcCompressionMode.Full,
                filter: x => x.Key.StartsWith("msgpack6c", StringComparison.Ordinal)));
        f.Should().BeEquivalentTo(formats, "a failed call must add nothing");
        f.DefaultFormat.Should().BeSameAs(f["msgpack6c-lz4f"]);

        var resolver = new RpcWireFormatResolver(f);
        resolver.DefaultFormat.Should().BeSameAs(f["msgpack6c-lz4f"]);
        resolver.Get("").Should().BeSameAs(resolver.DefaultFormat);
    }

    [Fact]
    public void AddLZ4VariantsShouldNotReplaceExistingFormatsTest()
    {
        var f = new RpcWireFormatConfiguration();
        var custom = RpcDefaultWireFormats.MessagePackV6C.WithLZ4("lz4");
        f.Add(custom);
        Assert.Throws<InvalidOperationException>(() => f.AddLZ4Variants("lz4"));
        f["msgpack6c-lz4"].Should().BeSameAs(custom);
        f.ContainsKey("msgpack6-lz4").Should().BeFalse();
    }

    [Fact]
    public void AddLZ4VariantsShouldUseLiteralSuffixTest()
    {
        var f = new RpcWireFormatConfiguration();
        f.AddLZ4Variants("lz4f", compressionMode: RpcCompressionMode.Full);
        f.AddLZ4Variants("x", filter: x => x.Key == "msgpack6");
        f["msgpack6c-lz4f"].CompressionMode.Should().Be(RpcCompressionMode.Full);
        f["msgpack6c-lz4f"].CompressionFormat.Should().BeSameAs(RpcCompressionFormat.LZ4);
        f["msgpack6-x"].CompressionMode.Should().Be(RpcCompressionMode.ServerToClient);
        f.ContainsKey("msgpack6c-lz4").Should().BeFalse();
        f.ContainsKey("msgpack6c-x").Should().BeFalse();
    }

    [Fact]
    public void ReplacingDefaultFormatRebindsItTest()
    {
        var f = new RpcWireFormatConfiguration();
        var source = RpcDefaultWireFormats.MessagePackV6;
        var replacement = new RpcWireFormat(source.Key,
            () => source.ArgumentListSerializer,
            source.MessageSerializerFactory,
            DataFormat.Bytes);
        f.Add(replacement);
        f.DefaultFormat.Should().BeSameAs(replacement);
        new RpcWireFormatResolver(f).DefaultFormat.Should().BeSameAs(replacement);
    }

    [Fact]
    public void ResolverRejectsUnregisteredDefaultFormatTest()
    {
        var f = new RpcWireFormatConfiguration();
        f.DefaultFormat = RpcDefaultWireFormats.MessagePackV6.WithLZ4("lz4");
        Assert.Throws<ArgumentException>(() => new RpcWireFormatResolver(f));

        f = new RpcWireFormatConfiguration();
        f.AddLZ4Variants("lz4");
        f.DefaultFormat = RpcDefaultWireFormats.MessagePackV6C.WithLZ4("lz4"); // Same key, other instance
        Assert.Throws<ArgumentException>(() => new RpcWireFormatResolver(f));
    }

    [Fact]
    public void ConfigureWireFormatsTest()
    {
        var services = new ServiceCollection();
        services.AddRpc().ConfigureWireFormats(f => {
            f.Remove("msgpack5", "msgpack5c");
            f.AddLZ4Variants("lz4");
            f.DefaultFormat = f["msgpack6c-lz4"];
            f.BackendOnlyFormatKeys = f.BackendOnlyFormatKeys.Add("json5np");
        });
        var c = services.BuildServiceProvider();

        var resolver = c.GetRequiredService<RpcWireFormatResolver>();
        resolver.DefaultFormat.Key.Should().Be("msgpack6c-lz4");
        resolver.TryGet("msgpack5", out _).Should().BeFalse();
        resolver.GetClientRejectionReason("msgpack5").Should().NotBeNull();
        resolver.GetClientRejectionReason("json5np").Should().NotBeNull();
        resolver.GetClientRejectionReason("msgpack6c-lz4").Should().BeNull();
        resolver.GetClientRejectionReason("").Should().BeNull();

        var configuration = c.GetRequiredService<RpcConfiguration>().WireFormats;
        configuration.IsFrozen.Should().BeTrue();
        Assert.ThrowsAny<Exception>(() => configuration.Remove("json5"));
    }
}
