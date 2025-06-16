using ActualLab.Fusion.Client.Caching;
using ActualLab.Fusion.Client.Internal;

namespace ActualLab.Fusion.Client;

public interface IRemoteComputedSynchronizer
{
    public Task WhenSynchronized(IRemoteComputed computed, CancellationToken cancellationToken);
}

public record RemoteComputedSynchronizer : IRemoteComputedSynchronizer
{
#if NET9_0_OR_GREATER
    private static readonly Lock StaticLock = new();
#else
    private static readonly object StaticLock = new();
#endif
    private static readonly AsyncLocal<IRemoteComputedSynchronizer?> CurrentLocal = new();
    private static volatile IRemoteComputedSynchronizer _default = new RemoteComputedSynchronizer();

    public static readonly IRemoteComputedSynchronizer? None = null;

    public static IRemoteComputedSynchronizer Default {
        get => _default;
        set {
            lock (StaticLock)
                _default = value;
        }
    }

    public static IRemoteComputedSynchronizer? Current {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => CurrentLocal.Value;
        internal set => CurrentLocal.Value = value;
    }

    public bool UseWhenDisconnected { get; init; }
    public bool UseWithRemoteComputedCacheHitToCallDelayer { get; init; }
    public Func<IRemoteComputed, CancellationToken, Task>? TimeoutFactory { get; init; }

    public virtual bool IsSynchronized(IRemoteComputed computed)
    {
        if (computed.WhenSynchronized.IsCompleted)
            return true;
        if (!(UseWhenDisconnected && !computed.Input.Function.Hub.RpcHub.DefaultPeer.IsConnected()))
            return true;
        if (!(UseWithRemoteComputedCacheHitToCallDelayer && RemoteComputedCache.HitToCallDelayer is not null))
            return true;

        return false;
    }

    public virtual Task WhenSynchronized(IRemoteComputed computed, CancellationToken cancellationToken)
    {
        if (IsSynchronized(computed))
            return Task.CompletedTask;

        return TimeoutFactory is { } timeoutFactory
            ? CompleteAsync(computed, timeoutFactory, cancellationToken)
            : computed.WhenSynchronized.WaitAsync(cancellationToken);

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
    public static RemoteComputeSynchronizerScope Activate(this IRemoteComputedSynchronizer? synchronizer)
        => new(synchronizer);
}
