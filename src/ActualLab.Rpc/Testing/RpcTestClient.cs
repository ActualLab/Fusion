using System.Globalization;

namespace ActualLab.Rpc.Testing;

public class RpcTestClient(IServiceProvider services) : RpcClient(services)
{
    private readonly ConcurrentDictionary<RpcPeerRef, RpcTestConnection> _connections = new();
    private long _lastPairId;

    public RpcTestClientOptions Options { get; init; } = services.GetRequiredService<RpcTestClientOptions>();

    public RpcTestConnection this[RpcPeerRef peerRef]
        => _connections.GetValueOrDefault(peerRef) ?? throw new KeyNotFoundException();

    public IReadOnlyDictionary<RpcPeerRef, RpcTestConnection> Connections => _connections;

    public RpcTestConnection CreateDefaultConnection(bool isBackend = false)
        => CreateConnection(RpcPeerRef.DefaultHostId, RpcPeerRef.DefaultHostId, isBackend);

    public RpcTestConnection CreateRandomConnection(bool isBackend = false)
    {
        var pairId = Interlocked.Increment(ref _lastPairId).ToString("x8", CultureInfo.InvariantCulture);
        return CreateConnection(pairId, pairId, isBackend);
    }

    public RpcTestConnection CreateConnection(string clientHostInfo, string serverHostInfo, bool isBackend = false)
    {
        var serializationFormatResolver = Services.GetRequiredService<RpcSerializationFormatResolver>();
        var defaultClientFormatKey = serializationFormatResolver.DefaultClientFormatKey;
        var serializationFormat = Options.SerializationFormatKey;
        if (serializationFormat.IsNullOrEmpty())
            serializationFormat = defaultClientFormatKey;
        var clientSerializationFormat =
            !string.Equals(serializationFormat, defaultClientFormatKey, StringComparison.Ordinal)
            ? serializationFormat
            : "";

        var clientPeerRef = RpcPeerRef.NewClient(clientHostInfo, clientSerializationFormat, isBackend);
        var serverPeerRef = RpcPeerRef.NewServer(serverHostInfo, serializationFormat, isBackend);
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
