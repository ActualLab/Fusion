using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc.Clients;

/// <summary>
/// An <see cref="RpcClient"/> that alternates remote connection attempts between inner clients.
/// </summary>
public class RpcAlternatingClient(IServiceProvider services, params RpcClient[] clients)
    : RpcClient(services)
{
    public RpcClient[] Clients { get; } = clients.Length != 0
        ? clients
        : throw new ArgumentOutOfRangeException(nameof(clients));

    public override async Task<RpcConnection> ConnectRemote(
        RpcClientPeer clientPeer,
        RpcPeerConnectionState connectionState,
        CancellationToken cancellationToken)
    {
        var state = GetOrAddState(clientPeer);
        var client = Clients.FirstOrDefault(x => !state.FailedClients.Contains(x)) ?? Clients.First();
        state.LastClient = client;
        state.LastConnectionStartedAt = Hub.SystemClock.Now;
        return await client.Connect(clientPeer, connectionState, cancellationToken).ConfigureAwait(false);
    }

    public override void OnConnectionStateChange(
        RpcClientPeer clientPeer,
        RpcPeerConnectionState connectionState)
    {
        var state = GetOrAddState(clientPeer);
        if (connectionState.IsConnected())
            state.FailedClients = default;
        else if (connectionState.IsDisconnected()) {
            try {
                if (state.LastClient is not { } lastClient)
                    return;
                if (!state.IsFailed(clientPeer, connectionState))
                    return;

                var failedClients = state.FailedClients.With(lastClient);
                if (failedClients.Distinct().Count() == Clients.Length)
                    failedClients = default;
                state.FailedClients = failedClients;
            }
            finally {
                state.LastClient = null;
                state.LastConnectionStartedAt = default;
            }
        }
    }

    // Protected methods

    protected virtual State CreateState() => new();

    // Private methods

    private State GetOrAddState(RpcClientPeer clientPeer)
    {
        var state = clientPeer.Extensions.KeylessGet<State>();
        if (state is not null)
            return state;

        state = CreateState();
        clientPeer.Extensions.KeylessSet(state);
        return state;
    }

    // Nested types

    public class State
    {
        public RpcClient? LastClient { get; set; }
        public Moment LastConnectionStartedAt { get; set; }
        public ApiArray<RpcClient> FailedClients { get; set; }

        public virtual bool IsFailed(RpcClientPeer clientPeer, RpcPeerConnectionState connectionState)
            => true;
    }
}
