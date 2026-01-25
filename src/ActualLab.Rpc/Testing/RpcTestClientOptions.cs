using ActualLab.Channels;
using ActualLab.IO;

namespace ActualLab.Rpc.Testing;

public record RpcTestClientOptions
{
    public static RpcTestClientOptions Default { get; set; } = new();

    public string SerializationFormatKey { get; init; } = "";
    public ChannelOptions ChannelOptions { get; init; } = new BoundedChannelOptions(500) {
        FullMode = BoundedChannelFullMode.Wait,
        SingleReader = true,
        SingleWriter = false,
        AllowSynchronousContinuations = false,
    };
    public Func<RpcTestClient, ChannelPair<ArrayOwner<byte>>> ConnectionFactory { get; init; } = DefaultConnectionFactory;

    // Protected methods

    protected static ChannelPair<ArrayOwner<byte>> DefaultConnectionFactory(RpcTestClient testClient)
    {
        var settings = testClient.Options;
        var channel1 = ChannelExt.Create<ArrayOwner<byte>>(settings.ChannelOptions);
        var channel2 = ChannelExt.Create<ArrayOwner<byte>>(settings.ChannelOptions);
        var connection = ChannelPair.CreateTwisted(channel1, channel2);
        return connection;
    }
}
