using ActualLab.Channels;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.WebSockets;

namespace ActualLab.Rpc.Testing;

public record RpcTestClientOptions
{
    public static RpcTestClientOptions Default { get; set; } = new();

    public string SerializationFormatKey { get; init; } = "";
    public ChannelOptions ChannelOptions { get; init; } = WebSocketChannel<RpcMessage>.Options.Default.WriteChannelOptions;
    public Func<RpcTestClient, ChannelPair<RpcMessage>> ConnectionFactory { get; init; } = DefaultConnectionFactory;

    // Protected methods

    protected static ChannelPair<RpcMessage> DefaultConnectionFactory(RpcTestClient testClient)
    {
        var settings = testClient.Options;
        var channel1 = ChannelExt.Create<RpcMessage>(settings.ChannelOptions);
        var channel2 = ChannelExt.Create<RpcMessage>(settings.ChannelOptions);
        var connection = ChannelPair.CreateTwisted(channel1, channel2);
        return connection;
    }
}
