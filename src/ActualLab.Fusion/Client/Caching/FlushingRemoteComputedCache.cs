using ActualLab.Rpc.Caching;

namespace ActualLab.Fusion.Client.Caching;

public abstract class FlushingRemoteComputedCache : RemoteComputedCache
{
    public new record Options(string Version = "") : RemoteComputedCache.Options(Version)
    {
        public static new Options Default { get; set; } = new();

        public TimeSpan FlushDelay { get; init; } = TimeSpan.FromSeconds(0.25);
        public MomentClock? Clock { get; init; }
    }

#if NET9_0_OR_GREATER
    protected readonly Lock Lock = new();
#else
    protected readonly object Lock = new();
#endif
    protected readonly MomentClock Clock;
    protected Dictionary<RpcCacheKey, RpcCacheValue> FlushQueue = new();
    protected Dictionary<RpcCacheKey, RpcCacheValue> FlushingQueue = new();
    protected Task? FlushTask;
    protected Task FlushingTask = Task.CompletedTask;
    protected CancellationTokenSource FlushCts = new();

    public new Options Settings { get; }

    protected FlushingRemoteComputedCache(Options settings, IServiceProvider services, bool initialize = true)
        : base(settings, services, false)
    {
        Settings = settings;
        Clock = settings.Clock ?? services.Clocks().CpuClock;
        if (initialize)
            // ReSharper disable once VirtualMemberCallInConstructor
            WhenInitialized = Initialize(settings.Version);
    }

    public override ValueTask<RpcCacheValue> Get(RpcCacheKey key, CancellationToken cancellationToken = default)
    {
        lock (Lock) {
            if (FlushQueue.TryGetValue(key, out var value))
                return new ValueTask<RpcCacheValue>(value);
            if (FlushingQueue.TryGetValue(key, out value))
                return new ValueTask<RpcCacheValue>(value);
        }
        return Fetch(key, cancellationToken);
    }

    public override void Set(RpcCacheKey key, RpcCacheValue value)
    {
        DefaultLog?.Log(Settings.LogLevel, "[+] {Key} = {Entry}", key, value);
        lock (Lock) {
            FlushQueue[key] = value;
            FlushTask ??= DelayedFlush(null, FlushCts.Token);
        }
    }

    public override void Remove(RpcCacheKey key)
    {
        DefaultLog?.Log(Settings.LogLevel, "[-] {Key}", key);
        lock (Lock) {
            FlushQueue[key] = default;
            FlushTask ??= DelayedFlush(null, FlushCts.Token);
        }
    }

    public Task Flush()
    {
        lock (Lock) {
            FlushCts.CancelAndDisposeSilently();
            FlushCts = new CancellationTokenSource();
            var flushTask = FlushTask ??= DelayedFlush(TimeSpan.Zero, CancellationToken.None);
            return flushTask;
        }
    }

    // Protected methods

    protected abstract ValueTask<RpcCacheValue> Fetch(RpcCacheKey key, CancellationToken cancellationToken);
    protected abstract Task Flush(Dictionary<RpcCacheKey, RpcCacheValue> flushingQueue);

    protected async Task DelayedFlush(TimeSpan? flushDelay, CancellationToken cancellationToken)
    {
        if (!WhenInitialized.IsCompleted)
            await WhenInitialized.WaitAsync(cancellationToken).SilentAwait(false);

        var delay = flushDelay ?? Settings.FlushDelay;
        if (delay > TimeSpan.Zero) {
            try {
                await Clock.Delay(delay, cancellationToken).SilentAwait(false);
            }
            catch {
                // Intended
            }
        }

#if NET9_0_OR_GREATER
        Lock.Enter();
#else
        Monitor.Enter(Lock);
#endif
        try {
            while (true) {
                var flushingTask = FlushingTask;
                if (flushingTask.IsCompleted)
                    break;

#if NET9_0_OR_GREATER
                Lock.Exit();
#else
                Monitor.Exit(Lock);
#endif
                try {
                    await flushingTask.SilentAwait(false);
                }
                finally {
#if NET9_0_OR_GREATER
                    Lock.Enter();
#else
                    Monitor.Enter(Lock);
#endif
                }
            }
            var flushingQueue = FlushingQueue = FlushQueue;
            FlushingTask = flushingQueue.Count == 0 ? Task.CompletedTask : Task.Run(() => Flush(flushingQueue), CancellationToken.None);
            FlushQueue = new();
            FlushTask = null;
        }
        finally {
#if NET9_0_OR_GREATER
            Lock.Exit();
#else
            Monitor.Exit(Lock);
#endif
        }
    }
}
