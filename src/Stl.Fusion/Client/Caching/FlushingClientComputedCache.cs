using Stl.Rpc.Caching;

namespace Stl.Fusion.Client.Caching;

public abstract class FlushingClientComputedCache : ClientComputedCache
{
    public new record Options : ClientComputedCache.Options
    {
        public static new Options Default { get; set; } = new();

        public TimeSpan FlushDelay { get; init; } = TimeSpan.FromSeconds(0.25);
        public IMomentClock? Clock { get; init; }
    }

    protected readonly object Lock = new();
    protected readonly IMomentClock Clock;
    protected Dictionary<RpcCacheKey, TextOrBytes?> FlushQueue = new();
    protected Dictionary<RpcCacheKey, TextOrBytes?> FlushingQueue = new();
    protected Task? FlushTask;
    protected Task FlushingTask = Task.CompletedTask;
    protected CancellationTokenSource FlushCts = new();

    public new Options Settings { get; }

    protected FlushingClientComputedCache(Options settings, IServiceProvider services, bool initialize = true)
        : base(settings, services, false)
    {
        Settings = settings;
        Clock = settings.Clock ?? services.Clocks().CpuClock;
        if (initialize)
            // ReSharper disable once VirtualMemberCallInConstructor
            WhenInitialized = Initialize(settings.Version);
    }

    public override ValueTask<TextOrBytes?> Get(RpcCacheKey key, CancellationToken cancellationToken = default)
    {
        lock (Lock) {
            if (FlushQueue.TryGetValue(key, out var value))
                return new ValueTask<TextOrBytes?>(value);
            if (FlushingQueue.TryGetValue(key, out value))
                return new ValueTask<TextOrBytes?>(value);
        }
        return Fetch(key, cancellationToken);
    }

    public override void Set(RpcCacheKey key, TextOrBytes value)
    {
        lock (Lock) {
            FlushQueue[key] = value;
            FlushTask ??= DelayedFlush(null, FlushCts.Token);
        }
    }

    public override void Remove(RpcCacheKey key)
    {
        lock (Lock) {
            FlushQueue[key] = null;
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

    protected abstract ValueTask<TextOrBytes?> Fetch(RpcCacheKey key, CancellationToken cancellationToken);
    protected abstract Task Flush(Dictionary<RpcCacheKey, TextOrBytes?> flushingQueue);

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

        Monitor.Enter(Lock);
        try {
            while (true) {
                var flushingTask = FlushingTask;
                if (flushingTask.IsCompleted)
                    break;

                Monitor.Exit(Lock);
                try {
                    await flushingTask.SilentAwait(false);
                }
                finally {
                    Monitor.Enter(Lock);
                }
            }
            var flushingQueue = FlushingQueue = FlushQueue;
            FlushingTask = flushingQueue.Count == 0 ? Task.CompletedTask : Task.Run(() => Flush(flushingQueue), CancellationToken.None);
            FlushQueue = new();
            FlushTask = null;
        }
        finally {
            Monitor.Exit(Lock);
        }
    }
}
