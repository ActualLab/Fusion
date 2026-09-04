using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Testing;

namespace ActualLab.Tests.Rpc;

/// <summary>
/// An <see cref="RpcTestClient"/> that connects to whichever <see cref="Connection"/> is set,
/// rather than the one matching the client peer ref. Lets a test drive one client peer through
/// connections to different server peers, i.e. simulate a peer change.
/// </summary>
public sealed class SwitchableRpcTestClient(IServiceProvider services) : RpcTestClient(services)
{
    public RpcTestConnection? Connection { get; set; }

    public override async Task<RpcConnection> ConnectRemote(
        RpcClientPeer clientPeer,
        RpcPeerConnectionState connectionState,
        CancellationToken cancellationToken)
    {
        var connection = Connection
            ?? throw new InvalidOperationException($"{nameof(Connection)} isn't set yet.");
        var transport = await connection.PullClientTransport(clientPeer, cancellationToken).ConfigureAwait(false);
        return new RpcConnection(transport);
    }
}
