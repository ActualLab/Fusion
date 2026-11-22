using System.Globalization;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc.Testing;

/// <summary>
/// An <see cref="RpcClient"/> implementation that creates in-memory channel-based connections for testing.
/// </summary>
public class RpcTestClient(IServiceProvider services) : RpcClient(services)
{
    private readonly ConcurrentDictionary<RpcRef, RpcTestConnection> _connections = new();
    private long _lastPairId;

    public RpcTestClientOptions Options { get; init; } = services.GetRequiredService<RpcTestClientOptions>();

    public RpcTestConnection this[RpcRef rpcRef]
        => _connections.GetValueOrDefault(rpcRef) ?? throw new KeyNotFoundException();

    public IReadOnlyDictionary<RpcRef, RpcTestConnection> Connections => _connections;

    // CreateXxx

    public RpcTestConnection CreateDefaultConnection(bool isBackend = false)
        => CreateConnection(RpcRef.DefaultHostId, RpcRef.DefaultHostId, isBackend);

    public RpcTestConnection CreateRandomConnection(bool isBackend = false)
    {
        var pairId = Interlocked.Increment(ref _lastPairId).ToString("x8", CultureInfo.InvariantCulture);
        return CreateConnection(pairId, pairId, isBackend);
    }

    public RpcTestConnection CreateConnection(string clientHostInfo, string serverHostInfo, bool isBackend = false)
    {
        var wireFormatResolver = Services.GetRequiredService<RpcWireFormatResolver>();
        var defaultClientFormatKey = wireFormatResolver.DefaultFormat.Key;
        var wireFormat = Options.WireFormatKey;
        if (wireFormat.IsNullOrEmpty())
            wireFormat = defaultClientFormatKey;
        var clientWireFormat =
            !string.Equals(wireFormat, defaultClientFormatKey, StringComparison.Ordinal)
            ? wireFormat
            : "";

        var clientPeerRef = RpcRef.NewClient(clientHostInfo, clientWireFormat, isBackend);
        var serverPeerRef = RpcRef.NewServer(serverHostInfo, wireFormat, isBackend);
        return CreateConnection(clientPeerRef, serverPeerRef);
    }

    public RpcTestConnection CreateConnection(RpcRef clientPeerRef, RpcRef serverPeerRef)
    {
        if (_connections.TryGetValue(clientPeerRef, out var connection))
            return connection;

        connection = new RpcTestConnection(this, clientPeerRef, serverPeerRef);
        _connections.TryAdd(clientPeerRef, connection);
        _connections.TryAdd(serverPeerRef, connection);
        return connection;
    }

    // FindConnection

    public RpcTestConnection GetConnection(RpcRef rpcRef)
        => Connections.First(kv => kv.Key == rpcRef).Value;
    public RpcTestConnection GetConnection(Func<RpcRef, bool> predicate)
        => Connections.First(kv => predicate.Invoke(kv.Key)).Value;

    // RpcClient implementation

    public override async Task<RpcConnection> ConnectRemote(
        RpcClientPeer clientPeer,
        RpcPeerConnectionState connectionState,
        CancellationToken cancellationToken)
    {
        var transport = await this[clientPeer.Ref].PullClientTransport(clientPeer, cancellationToken).ConfigureAwait(false);
        return new RpcConnection(transport);
    }
}
