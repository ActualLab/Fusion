using ActualLab.OS;

namespace ActualLab.Fusion.Internal;

/// <summary>
/// A background worker that periodically prunes stale edges and disposed instances
/// from the <see cref="ComputedRegistry"/> dependency graph.
/// </summary>
public sealed class ComputedGraphPruner : WorkerBase
{
    /// <summary>
    /// Global configuration settings for <see cref="ComputedGraphPruner"/>.
    /// </summary>
    public static class Settings
    {
        public static bool AutoActivate { get; set; } = true;
        public static bool MustInvokePrune { get; set; } = true;
        public static int BatchSize { get; set; } = HardwareInfo.ProcessorCountPo2 * 512;
        public static RandomTimeSpan CheckPeriod { get; set; } = TimeSpan.FromMinutes(5).ToRandom(0.1);
        public static RandomTimeSpan InterBatchDelay { get; set; } = TimeSpan.FromSeconds(0.1).ToRandom(0.25);
        public static RetryDelaySeq RetryDelays { get; set; } = RetryDelaySeq.Exp(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(10));
        public static TimeSpan DisposedComputedInvalidationDelay { get; set; } = TimeSpan.FromSeconds(5);
        public static ILogger? Log { get; set; }
    }

    internal static readonly ComputedRegistry.MeterSet Metrics = ComputedRegistry.Metrics;

    private ILogger Log { get; init; }

    public ComputedGraphPruner(ILogger<ComputedGraphPruner>? log = null)
    {
        Log = log ?? Log ?? StaticLog.For(GetType());
        if (Settings.AutoActivate)
            this.Start();
    }

    public Task PruneOnce(CancellationToken cancellationToken)
        => CreatePruneOnceChain().Run(cancellationToken);

    // Protected methods

    protected override async Task OnRun(CancellationToken cancellationToken)
    {
        if (ComputedRegistry.ChangeGraphPruner(this, out var prevPruner) && prevPruner is not null)
            await prevPruner.DisposeAsync().SilentAwait(false);

        await Task.Delay(Settings.CheckPeriod.Next(), cancellationToken).ConfigureAwait(false);
        var chain = CreatePruneOnceChain()
            .AppendDelay(Settings.CheckPeriod)
            .RetryForever(Settings.RetryDelays)
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
        var batchSize = Settings.BatchSize;
        var interBatchDelay = Settings.InterBatchDelay;
        var disposedComputedInvalidationDelay = Settings.DisposedComputedInvalidationDelay;

        using var keyEnumerator = ComputedRegistry.Keys.GetEnumerator();
        var disposedCount = 0L;
        var remainingBatchCapacity = batchSize;
        var batchCount = 0;
        var invalidationSource = InvalidationSource.ForCurrentLocation();
        while (keyEnumerator.MoveNext()) {
            if (--remainingBatchCapacity <= 0) {
                var pausedAt = CpuTimestamp.Now;
                await Task.Delay(interBatchDelay.Next(), cancellationToken).ConfigureAwait(false);
                startedAt += pausedAt.Elapsed;
                remainingBatchCapacity = batchSize;
                batchCount++;
            }

            var computedInput = keyEnumerator.Current!;
            if (ComputedRegistry.Get(computedInput) is { } c && c.IsConsistent() && computedInput.IsDisposed) {
                disposedCount++;
                c.Invalidate(disposedComputedInvalidationDelay, invalidationSource);
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
        using var keyEnumerator = ComputedRegistry.Keys.GetEnumerator();
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
                await Task.Delay(Settings.InterBatchDelay.Next(), ct).ConfigureAwait(false);
                startedAt += pausedAt.Elapsed;
                remainingBatchCapacity = Settings.BatchSize;
                batchCount++;
            }

            var computedInput = keyEnumerator.Current!;
            computedCount++;
            if (ComputedRegistry.Get(computedInput) is { } c && c.IsConsistent()) {
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

        await Task.Delay(Settings.InterBatchDelay.Next(), ct).ConfigureAwait(false);
        if (Settings.MustInvokePrune)
            await ComputedRegistry.Prune().ConfigureAwait(false);
    }
}
