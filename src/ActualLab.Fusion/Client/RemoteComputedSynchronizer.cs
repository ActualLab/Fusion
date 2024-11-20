using ActualLab.Fusion.Client.Internal;
using ActualLab.Rpc;

namespace ActualLab.Fusion.Client;

public interface IRemoteComputedSynchronizer
{
    public Task WhenSynchronized(Computed computed, CancellationToken cancellationToken);
}

public record RemoteComputedSynchronizer : IRemoteComputedSynchronizer
{
    private static readonly AsyncLocal<IRemoteComputedSynchronizer?> CurrentLocal = new();

    public static readonly IRemoteComputedSynchronizer? None = null;
    public static IRemoteComputedSynchronizer Default { get; set; } = new RemoteComputedSynchronizer();

    public static IRemoteComputedSynchronizer? Current {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => CurrentLocal.Value;
        internal set => CurrentLocal.Value = value;
    }

    public bool UseWhenDisconnected { get; init; } // = Unused when disconnected by default!
    public Func<IRemoteComputed, RpcPeer> PeerResolver { get; init; } =
        static c => c.Input.Function.Hub.RpcHub.DefaultPeer;
    public Func<IRemoteComputed, CancellationToken, Task>? TimeoutFactory { get; init; }

    public virtual Task WhenSynchronized(Computed computed, CancellationToken cancellationToken)
    {
        if (computed is not IRemoteComputed remoteComputed)
            return Task.CompletedTask;

        var whenSynchronized = remoteComputed.WhenSynchronized;
        if (whenSynchronized.IsCompleted)
            return Task.CompletedTask;

        if (!UseWhenDisconnected) {
            var peer = PeerResolver.Invoke(remoteComputed);
            if (!peer.IsConnected())
                return Task.CompletedTask;
        }

        return TimeoutFactory is { } timeoutFactory
            ? CompleteAsync(remoteComputed, timeoutFactory, cancellationToken)
            : remoteComputed.WhenSynchronized.WaitAsync(cancellationToken);

        static async Task CompleteAsync(
            IRemoteComputed computed,
            Func<IRemoteComputed, CancellationToken, Task> timeoutFactory,
            CancellationToken cancellationToken)
        {
            var cts = cancellationToken.CreateLinkedTokenSource();
            try {
                var timeoutTask = timeoutFactory.Invoke(computed, cts.Token);
                if (!timeoutTask.IsCompletedSuccessfully())
                    await Task.WhenAny(computed.WhenSynchronized, timeoutTask).ConfigureAwait(false);
            }
            finally {
                cts.CancelAndDisposeSilently();
            }
        }
    }
}

public static class RemoteComputedSynchronizerExt
{
    public static RemoteComputeSynchronizerScope Activate(this IRemoteComputedSynchronizer synchronizer)
        => new(synchronizer);
}
