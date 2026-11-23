using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Serialization.Internal;
using MessagePack;

namespace ActualLab.Tests.Serialization;

public class KeyedMessagePackTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public void AppTypesShouldBeStringKeyedMaps()
    {
        // arrange
        var dto = NewDto();

        // act
        var keyedJson = ToJson(MessagePackByteSerializer.DefaultKeyed, dto);
        var defaultJson = ToJson(MessagePackByteSerializer.Default, dto);

        // assert
        Out.WriteLine(keyedJson);
        keyedJson.Should().StartWith("{").And.Contain("\"Name\":\"A\"").And.Contain("\"Count\":3");
        keyedJson.Should().Contain("\"Items\":[{\"Value\":\"x\"}]", "nested app types are keyed too");
        defaultJson.Should().StartWith("[\"A\",3,");
    }

    [Fact]
    public void KeyedSerializerShouldRoundTrip()
    {
        // arrange
        var dto = NewDto();

        // act
        using var buffer = MessagePackByteSerializer.DefaultKeyed.Write(dto, typeof(KeyedDto));
        var data = buffer.WrittenMemory;
        var copy = MessagePackByteSerializer.DefaultKeyed.Read<KeyedDto>(ref data);

        // assert
        copy.Name.Should().Be(dto.Name);
        copy.Count.Should().Be(dto.Count);
        copy.Option.Should().Be(dto.Option);
        copy.Time.Should().Be(dto.Time);
        copy.Items.Select(x => x.Value).Should().Equal("x");
    }

    [Fact]
    public void FrameworkTypesShouldKeepTheirLayout()
    {
        // arrange
        var handshake = new RpcHandshake(Guid.NewGuid(), null, Guid.NewGuid(), 1, 0);

        // act
        var keyedJson = ToJson(MessagePackByteSerializer.DefaultKeyed, handshake);
        var defaultJson = ToJson(MessagePackByteSerializer.Default, handshake);

        // assert
        StringKeyMessagePackResolver.IsFrameworkAssembly(typeof(RpcHandshake).Assembly).Should().BeTrue();
        StringKeyMessagePackResolver.IsFrameworkAssembly(typeof(KeyedDto).Assembly).Should().BeFalse();
        keyedJson.Should().Be(defaultJson);
    }

    [Fact]
    public void AddKeyedMessagePackShouldAddKeyedFormats()
    {
        // arrange
        var formats = new RpcWireFormatConfiguration();

        // act
        formats.AddKeyedMessagePack();
        formats.AddLZ4Variants("lz4", filter: x => x.Key.EndsWith("k", StringComparison.Ordinal));

        // assert
        formats["msgpack6k"].Should().BeSameAs(RpcDefaultWireFormats.MessagePackV6K);
        formats["msgpack6ck"].Should().BeSameAs(RpcDefaultWireFormats.MessagePackV6CK);
        formats.ContainsKey("msgpack6ck-lz4").Should().BeTrue();
        formats.ContainsKey("msgpack6c-lz4").Should().BeFalse();
        Assert.Throws<InvalidOperationException>(() => formats.AddKeyedMessagePack());
    }

    // Private methods

    private static KeyedDto NewDto()
        => new() {
            Name = "A",
            Count = 3,
            Option = Option.Some(5),
            Time = new Moment(DateTime.UtcNow),
            Items = [new KeyedItem { Value = "x" }],
        };

    private static string ToJson<T>(MessagePackByteSerializer serializer, T value)
    {
        using var buffer = serializer.Write(value, typeof(T));
        return MessagePackSerializer.ConvertToJson(buffer.WrittenMemory);
    }
}

[DataContract, MessagePackObject]
public sealed class KeyedDto
{
    [DataMember(Order = 0), Key(0)] public string Name { get; set; } = "";
    [DataMember(Order = 1), Key(1)] public int Count { get; set; }
    [DataMember(Order = 2), Key(2)] public Option<int> Option { get; set; }
    [DataMember(Order = 3), Key(3)] public Moment Time { get; set; }
    [DataMember(Order = 4), Key(4)] public List<KeyedItem> Items { get; set; } = [];
}

[DataContract, MessagePackObject]
public sealed class KeyedItem
{
    [DataMember(Order = 0), Key(0)] public string Value { get; set; } = "";
}
