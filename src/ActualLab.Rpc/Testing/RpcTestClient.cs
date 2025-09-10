using System.Globalization;
using ActualLab.Channels;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.WebSockets;

namespace ActualLab.Rpc.Testing;

public class RpcTestClient(
    RpcTestClient.Options settings,
    IServiceProvider services
    ) : RpcClient(services)
{
    public record Options
    {
        public static Options Default { get; set; } = new();

        public string SerializationFormatKey { get; init; } = "";
        public ChannelOptions ChannelOptions { get; init; } = WebSocketChannel<RpcMessage>.Options.Default.WriteChannelOptions;
        public Func<RpcTestClient, ChannelPair<RpcMessage>> ConnectionFactory { get; init; } = DefaultConnectionFactory;

        public static ChannelPair<RpcMessage> DefaultConnectionFactory(RpcTestClient testClient)
        {
            var settings = testClient.Settings;
            var channel1 = ChannelExt.Create<RpcMessage>(settings.ChannelOptions);
            var channel2 = ChannelExt.Create<RpcMessage>(settings.ChannelOptions);
            var connection = ChannelPair.CreateTwisted(channel1, channel2);
            return connection;
        }
    }

    private readonly ConcurrentDictionary<RpcPeerRef, RpcTestConnection> _connections = new();
    private long _lastPairId;

    public Options Settings { get; init; } = settings;
    public new RpcHub Hub => base.Hub;

    public RpcTestConnection this[RpcPeerRef peerRef]
        => _connections.GetValueOrDefault(peerRef) ?? throw new KeyNotFoundException();

    public IReadOnlyDictionary<RpcPeerRef, RpcTestConnection> Connections => _connections;

    public RpcTestConnection CreateDefaultConnection()
        => CreateConnection(RpcPeerRef.DefaultHostId, RpcPeerRef.DefaultHostId);

    public RpcTestConnection CreateRandomConnection()
    {
        var pairId = Interlocked.Increment(ref _lastPairId).ToString("x8", CultureInfo.InvariantCulture);
        return CreateConnection(pairId, pairId);
    }

    public RpcTestConnection CreateConnection(string clientHostInfo, string serverHostInfo)
    {
        var serializationFormatResolver = Services.GetRequiredService<RpcSerializationFormatResolver>();
        var defaultClientFormatKey = serializationFormatResolver.DefaultClientFormatKey;
        var serializationFormat = Settings.SerializationFormatKey;
        if (serializationFormat.IsNullOrEmpty())
            serializationFormat = defaultClientFormatKey;
        var clientSerializationFormat =
            !string.Equals(serializationFormat, defaultClientFormatKey, StringComparison.Ordinal)
            ? serializationFormat
            : "";

        var clientPeerRef = RpcPeerRef.NewClient(clientHostInfo, clientSerializationFormat);
        var serverPeerRef = RpcPeerRef.NewServer(serverHostInfo, serializationFormat);
        return CreateConnection(clientPeerRef, serverPeerRef);
    }

    public RpcTestConnection CreateConnection(RpcPeerRef clientPeerRef, RpcPeerRef serverPeerRef)
    {
        if (_connections.TryGetValue(clientPeerRef, out var connection))
            return connection;

        connection = new RpcTestConnection(this, clientPeerRef, serverPeerRef);
        _connections.TryAdd(clientPeerRef, connection);
        _connections.TryAdd(serverPeerRef, connection);
        return connection;
    }

    public override async Task<RpcConnection> ConnectRemote(RpcClientPeer clientPeer, CancellationToken cancellationToken)
    {
        var channel = await this[clientPeer.Ref].PullClientChannel(cancellationToken).ConfigureAwait(false);
        return new RpcConnection(channel);
    }
}
