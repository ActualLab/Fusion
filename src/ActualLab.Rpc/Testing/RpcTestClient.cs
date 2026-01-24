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

    // CreateXxx

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
        var defaultClientFormatKey = serializationFormatResolver.DefaultFormatKey;
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

    // FindConnection

    public RpcTestConnection GetConnection(RpcPeerRef peerRef)
        => Connections.First(kv => kv.Key == peerRef).Value;
    public RpcTestConnection GetConnection(Func<RpcPeerRef, bool> predicate)
        => Connections.First(kv => predicate.Invoke(kv.Key)).Value;

    // RpcClient implementation

    public override async Task<RpcConnection> ConnectRemote(RpcClientPeer clientPeer, CancellationToken cancellationToken)
    {
        var transport = await this[clientPeer.Ref].PullClientTransport(clientPeer, cancellationToken).ConfigureAwait(false);
        return new RpcConnection(transport);
    }
}
