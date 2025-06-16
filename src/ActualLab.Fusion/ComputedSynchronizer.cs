using ActualLab.Fusion.Client;
using ActualLab.Fusion.Client.Caching;
using ActualLab.Fusion.Internal;

namespace ActualLab.Fusion;

public abstract class ComputedSynchronizer
{
#if NET9_0_OR_GREATER
    private static readonly Lock StaticLock = new();
#else
    private static readonly object StaticLock = new();
#endif
    internal static readonly AsyncLocal<ComputedSynchronizer?> CurrentLocal = new();

    public static ComputedSynchronizer Default { get; set; } = new Safe();
    public static ComputedSynchronizer DefaultCurrent { get; set; } = None.Instance;

    public static ComputedSynchronizer Current {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => CurrentLocal.Value ?? DefaultCurrent;
        internal set => CurrentLocal.Value = value;
    }

    public int MaxUpdateCountOnSynchronize { get; init; }

    public ComputeSynchronizerScope Activate()
        => new(this);

    public virtual bool IsSynchronized(Computed computed)
    {
        if (computed is IRemoteComputed remoteComputed)
            return IsSynchronized(remoteComputed);

        if (computed is IStateBoundComputed stateBoundComputed) {
            var state = stateBoundComputed.State;
            if (state is IMutableState)
                return true;

            var snapshot = state.Snapshot;
            if (snapshot.IsInitial)
                return false;
        }

        // Computed is a regular computed instance
        var usedBuffer = ArrayBuffer<Computed>.Lease(false);
        try {
            computed.CopyDependenciesTo(ref usedBuffer);
            var usedArray = usedBuffer.Buffer;
            for (var i = 0; i < usedBuffer.Count; i++) {
                if (!IsSynchronized(usedArray[i]))
                    return false;
            }
            return true;
        }
        finally {
            usedBuffer.Release();
        }
    }

    public virtual Task WhenSynchronized(Computed computed, CancellationToken cancellationToken)
    {
        if (computed is IRemoteComputed remoteComputed)
            return WhenSynchronized(remoteComputed, cancellationToken);

        if (computed is IStateBoundComputed stateBoundComputed) {
            var state = stateBoundComputed.State;
            if (state is IMutableState)
                return Task.CompletedTask;

            var snapshot = state.Snapshot;
            if (snapshot.IsInitial)
                return WhenUpdatedAndSynchronized(this, snapshot, cancellationToken);

            static async Task WhenUpdatedAndSynchronized(
                ComputedSynchronizer self,
                StateSnapshot snapshot,
                CancellationToken cancellationToken1)
            {
                await snapshot.WhenUpdated().WaitAsync(cancellationToken1).ConfigureAwait(false);
                await self.WhenSynchronized(snapshot.State.Computed, cancellationToken1).ConfigureAwait(false);
            }
        }

        // Computed is a regular computed instance
        var usedBuffer = ArrayBuffer<Computed>.Lease(false);
        var taskBuffer = ArrayBuffer<Task>.Lease(false);
        try {
            computed.CopyDependenciesTo(ref usedBuffer);
            var usedArray = usedBuffer.Buffer;
            for (var i = 0; i < usedBuffer.Count; i++) {
                var used = usedArray[i];
                var whenSynchronized = WhenSynchronized(used, cancellationToken);
                if (!whenSynchronized.IsCompleted)
                    taskBuffer.Add(whenSynchronized);
            }
            return taskBuffer.Count switch {
                0 => Task.CompletedTask,
                1 => taskBuffer[0],
                _ => Task.WhenAll(taskBuffer.ToArray()),
            };
        }
        finally {
            taskBuffer.Release();
            usedBuffer.Release();
        }
    }

    public async ValueTask<Computed> Synchronize(Computed computed, CancellationToken cancellationToken)
    {
        for (var updateCount = 0;; updateCount++) {
            var whenSynchronized = WhenSynchronized(computed, cancellationToken);
            if (!whenSynchronized.IsCompleted)
                await whenSynchronized.SilentAwait(false);
            if (!whenSynchronized.IsCompletedSuccessfully())
                return computed; // Timed out
            if (computed.IsConsistent() || updateCount >= MaxUpdateCountOnSynchronize)
                return computed;

            computed = await computed.UpdateUntyped(cancellationToken).ConfigureAwait(false);
        }
    }

    public abstract bool IsSynchronized(IRemoteComputed computed);
    public abstract Task WhenSynchronized(IRemoteComputed computed, CancellationToken cancellationToken);

    // Nested types

    public sealed class None : ComputedSynchronizer
    {
        public static None Instance { get; } = new();

        public override bool IsSynchronized(Computed computed)
            => true;
        public override bool IsSynchronized(IRemoteComputed computed)
            => true;

        public override Task WhenSynchronized(Computed computed, CancellationToken cancellationToken)
            => Task.CompletedTask;
        public override Task WhenSynchronized(IRemoteComputed computed, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    public sealed class Precise : ComputedSynchronizer
    {
        public static Precise Instance { get; set; } = new();

        public Precise()
            => MaxUpdateCountOnSynchronize = 1;

        public override bool IsSynchronized(IRemoteComputed computed)
            => computed.WhenSynchronized.IsCompleted;

        public override Task WhenSynchronized(IRemoteComputed computed, CancellationToken cancellationToken)
            => computed.WhenSynchronized.WaitAsync(cancellationToken);
    }

    public class Safe : ComputedSynchronizer
    {
        public static Safe Instance { get; set; } = new();

        public bool AssumeSynchronizedWhenDisconnected { get; init; } = true;
        public bool AssumeSynchronizedWhenRemoteComputedCacheHasHitToCallDelayer { get; init; } = true;
        public Func<IRemoteComputed, TimeSpan?>? MaxSynchronizeDurationProvider { get; init; } = static _ => TimeSpan.FromSeconds(5);

        public bool AssumeSynchronized {
            get;
            set => Interlocked.Exchange(ref field, value);
        }

        public Safe()
            => MaxUpdateCountOnSynchronize = 1;

        public override bool IsSynchronized(IRemoteComputed computed)
        {
            if (computed.WhenSynchronized.IsCompleted)
                return true;
            if (AssumeSynchronized)
                return true;
            if (AssumeSynchronizedWhenRemoteComputedCacheHasHitToCallDelayer && RemoteComputedCache.HitToCallDelayer is not null)
                return true;
            if (AssumeSynchronizedWhenDisconnected && !computed.Input.Function.Hub.RpcHub.DefaultPeer.IsConnected())
                return true;

            return false;
        }

        public override Task WhenSynchronized(IRemoteComputed computed, CancellationToken cancellationToken)
        {
            if (IsSynchronized(computed))
                return Task.CompletedTask;

            var whenSynchronized = computed.WhenSynchronized;
            if (whenSynchronized.IsCompleted)
                return whenSynchronized;
            return MaxSynchronizeDurationProvider?.Invoke(computed) is { } maxSynchronizeDuration
                ? WhenSynchronizedAsync(whenSynchronized, maxSynchronizeDuration, cancellationToken)
                : whenSynchronized.WaitAsync(cancellationToken);

            static async Task WhenSynchronizedAsync(Task whenSynchronized, TimeSpan maxSynchronizeDuration, CancellationToken cancellationToken)
            {
                var cts = cancellationToken.CreateLinkedTokenSource();
                try {
                    var delayTask = maxSynchronizeDuration >= TimeSpan.Zero
                        ? Task.Delay(maxSynchronizeDuration, cts.Token)
                        : Task.CompletedTask;
                    var completedTask = await Task.WhenAny(whenSynchronized, delayTask).ConfigureAwait(false);
                    await completedTask.ConfigureAwait(false);
                }
                finally {
                    cts.CancelAndDisposeSilently();
                }
            }
        }
    }
}
