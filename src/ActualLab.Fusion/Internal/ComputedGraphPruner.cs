namespace ActualLab.Fusion.Internal;

public sealed class ComputedGraphPruner : WorkerBase
{
    public record Options
    {
        public static Options Default { get; set; } = new();

        public bool AutoActivate { get; init; } = true;
        public bool MustPruneRegistry { get; init; } = true;
        public int BatchSize { get; init; } = FusionDefaults.ComputedGraphPrunerBatchSize;
        public RandomTimeSpan CheckPeriod { get; init; } = TimeSpan.FromMinutes(5).ToRandom(0.1);
        public RandomTimeSpan InterBatchDelay { get; init; } = TimeSpan.FromSeconds(0.1).ToRandom(0.25);
        public RetryDelaySeq RetryDelays { get; init; } = RetryDelaySeq.Exp(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(10));
        public TimeSpan DisposedComputedInvalidationDelay { get; init; } = TimeSpan.FromSeconds(5);
    }

    internal static readonly ComputedRegistry.MeterSet Metrics = ComputedRegistry.Metrics;
    private readonly TaskCompletionSource<Unit> _whenActivatedSource;

    public Options Settings { get; init; }
    public MomentClock Clock { get; init; }
    public ILogger Log { get; init; }

    public Task<Unit> WhenActivated => _whenActivatedSource.Task;

    public ComputedGraphPruner(Options settings, ILogger<ComputedGraphPruner>? log = null)
        : this(settings, MomentClockSet.Default, log) { }
    public ComputedGraphPruner(Options settings, IServiceProvider services)
        : this(settings, services.Clocks(), services.LogFor<ComputedGraphPruner>()) { }
    public ComputedGraphPruner(Options settings, MomentClockSet clocks, ILogger<ComputedGraphPruner>? log = null)
    {
        Settings = settings;
        Clock = clocks.CpuClock;
        Log = log ?? StaticLog.For(GetType());
        _whenActivatedSource = TaskCompletionSourceExt.New<Unit>();

        if (settings.AutoActivate)
            this.Start();
    }

    public Task PruneOnce(CancellationToken cancellationToken)
        => CreatePruneOnceChain().Run(cancellationToken);

    // Protected methods

    protected override async Task OnRun(CancellationToken cancellationToken)
    {
        var computedRegistry = ComputedRegistry.Instance;
        if (Settings.AutoActivate) {
            // This prevents race condition when two pruners are assigned at almost
            // the same time - they'll both may end up activate themselves here
            var oldGraphPruner = computedRegistry.GraphPruner;
            while (oldGraphPruner != this) {
                await oldGraphPruner.WhenActivated.ConfigureAwait(false);
                oldGraphPruner = computedRegistry.ChangeGraphPruner(this, oldGraphPruner);
            }
        }
        else if (computedRegistry.GraphPruner != this) {
            Log.LogWarning("Terminating: ComputedRegistry.Instance.GraphPruner != this");
            return;
        }
        _whenActivatedSource.TrySetResult(default);

        await Clock.Delay(Settings.CheckPeriod.Next(), cancellationToken).ConfigureAwait(false);
        var chain = CreatePruneOnceChain()
            .AppendDelay(Settings.CheckPeriod, Clock)
            .RetryForever(Settings.RetryDelays, Clock)
            .CycleForever()
            .Log(Log);
        cancellationToken.ThrowIfCancellationRequested();
        await chain.Start(cancellationToken).ConfigureAwait(false);
    }

    // Private methods

    private AsyncChain CreatePruneOnceChain()
        => new AsyncChain(nameof(PruneDisposedInstances), PruneDisposedInstances).Silence()
            .Append(new AsyncChain(nameof(PruneEdges), PruneEdges).Silence());

    private async Task PruneDisposedInstances(CancellationToken cancellationToken)
    {
        var startedAt = CpuTimestamp.Now;
        Metrics.NodeEdgePruneCount.Add(1);
        var registry = ComputedRegistry.Instance;
        var batchSize = Settings.BatchSize;
        var interBatchDelay = Settings.InterBatchDelay;
        var disposedComputedInvalidationDelay = Settings.DisposedComputedInvalidationDelay;

        using var keyEnumerator = registry.Keys.GetEnumerator();
        var disposedCount = 0L;
        var remainingBatchCapacity = batchSize;
        var batchCount = 0;
        while (keyEnumerator.MoveNext()) {
            if (--remainingBatchCapacity <= 0) {
                var pausedAt = CpuTimestamp.Now;
                await Clock.Delay(interBatchDelay.Next(), cancellationToken).ConfigureAwait(false);
                startedAt += pausedAt.Elapsed;
                remainingBatchCapacity = batchSize;
                batchCount++;
            }

            var computedInput = keyEnumerator.Current!;
            if (registry.Get(computedInput) is { } c && c.IsConsistent() && computedInput.IsDisposed) {
                disposedCount++;
                c.Invalidate(disposedComputedInvalidationDelay);
            }
        }
        if (disposedCount == 0)
            return;

        Metrics.PrunedDisposedCount.Add(disposedCount);
        Metrics.NodePruneDuration.Record(startedAt.Elapsed.TotalMilliseconds);
        Log.LogInformation(
            "Removed {DisposedCount} instances originating from disposed compute services " +
            "in {BatchCount} batches (x {BatchSize})",
            disposedCount, batchCount + 1, batchSize);
    }

    private async Task PruneEdges(CancellationToken ct)
    {
        var startedAt = CpuTimestamp.Now;
        var registry = ComputedRegistry.Instance;
        using var keyEnumerator = registry.Keys.GetEnumerator();
        var computedCount = 0L;
        var consistentCount = 0L;
        var edgeCount = 0L;
        var removedEdgeCount = 0L;
        var remainingBatchCapacity = Settings.BatchSize;
        var batchCount = 0;
        CpuTimestamp pausedAt;
        while (keyEnumerator.MoveNext()) {
            if (--remainingBatchCapacity <= 0) {
                pausedAt = CpuTimestamp.Now;
                await Clock.Delay(Settings.InterBatchDelay.Next(), ct).ConfigureAwait(false);
                startedAt += pausedAt.Elapsed;
                remainingBatchCapacity = Settings.BatchSize;
                batchCount++;
            }

            var computedInput = keyEnumerator.Current!;
            computedCount++;
            if (registry.Get(computedInput) is { } c && c.IsConsistent()) {
                consistentCount++;
                var (oldEdgeCount, newEdgeCount) = c.PruneDependants();
                edgeCount += oldEdgeCount;
                removedEdgeCount += oldEdgeCount - newEdgeCount;
            }
        }
        Interlocked.Exchange(ref Metrics.NodeCount, computedCount);
        Interlocked.Exchange(ref Metrics.EdgeCount, edgeCount);
        Metrics.PrunedEdgeCount.Add(removedEdgeCount);
        Metrics.EdgePruneDuration.Record(startedAt.Elapsed.TotalMilliseconds);
        Log.LogInformation(
            "Processed {ConsistentCount}/{ComputedCount} instances, " +
            "removed {RemovedEdgeCount}/{EdgeCount} dependency-to-dependant edges, " +
            "in {BatchCount} batches (x {BatchSize})",
            consistentCount, computedCount, removedEdgeCount, edgeCount, batchCount + 1, Settings.BatchSize);

        await Clock.Delay(Settings.InterBatchDelay.Next(), ct).ConfigureAwait(false);
        if (Settings.MustPruneRegistry)
            await registry.Prune().ConfigureAwait(false);
    }
}
