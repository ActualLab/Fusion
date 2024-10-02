using System.Diagnostics.CodeAnalysis;
using ActualLab.Channels;
using ActualLab.Internal;
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

        public Symbol SerializationFormatKey { get; init; }
        public BoundedChannelOptions ChannelOptions { get; init; } = WebSocketChannel<RpcMessage>.Options.Default.WriteChannelOptions;
        public Func<RpcTestClient, ChannelPair<RpcMessage>> ConnectionFactory { get; init; } = DefaultConnectionFactory;

        public static ChannelPair<RpcMessage> DefaultConnectionFactory(RpcTestClient testClient)
        {
            var settings = testClient.Settings;
            var channel1 = Channel.CreateBounded<RpcMessage>(settings.ChannelOptions);
            var channel2 = Channel.CreateBounded<RpcMessage>(settings.ChannelOptions);
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
        => CreateConnection(RpcPeerRef.Default.Key, "server-default");

    public RpcTestConnection CreateRandomConnection()
    {
        var pairId = Interlocked.Increment(ref _lastPairId);
        return CreateConnection($"client-{pairId}", $"server-{pairId}");
    }

    public RpcTestConnection CreateConnection(string clientId, string serverId)
    {
        var serializationFormatResolver = Services.GetRequiredService<RpcSerializationFormatResolver>();
        var serializationFormatKey = Settings.SerializationFormatKey;
        if (serializationFormatKey.IsEmpty)
            serializationFormatKey = serializationFormatResolver.DefaultClientFormatKey;

        var clientPeerRef = serializationFormatKey == serializationFormatResolver.DefaultClientFormatKey
            ? RpcPeerRef.NewClient(clientId)
            : RpcPeerRef.NewClient(clientId, serializationFormatKey);
        var serverPeerRef = RpcPeerRef.NewServer(serverId, serializationFormatKey);
        return CreateConnection(clientPeerRef, serverPeerRef);
    }

    public RpcTestConnection CreateConnection(RpcPeerRef clientPeerRef, RpcPeerRef serverPeerRef)
    {
        if (_connections.TryGetValue(clientPeerRef, out var peerState))
            return peerState;

        peerState = new RpcTestConnection(this, clientPeerRef, serverPeerRef);
        _connections.TryAdd(clientPeerRef, peerState);
        _connections.TryAdd(serverPeerRef, peerState);
        return peerState;
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public override async Task<RpcConnection> ConnectRemote(RpcClientPeer clientPeer, CancellationToken cancellationToken)
    {
        var channel = await this[clientPeer.Ref].PullClientChannel(cancellationToken).ConfigureAwait(false);
        return new RpcConnection(channel);
    }
}
