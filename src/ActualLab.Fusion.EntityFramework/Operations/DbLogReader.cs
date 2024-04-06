using Microsoft.EntityFrameworkCore;
using ActualLab.OS;

namespace ActualLab.Fusion.EntityFramework.Operations;

public abstract class DbLogReader<TDbContext>(DbLogReader<TDbContext>.Options settings, IServiceProvider services)
    : DbShardWorkerBase<TDbContext>(services)
    where TDbContext : DbContext
{
    public record Options
    {
        public int BatchSize { get; init; } = 256;
        public TimeSpan MaxCommitDuration { get; init; } = TimeSpan.FromSeconds(1);
        public RandomTimeSpan UnconditionalCheckPeriod { get; init; } = TimeSpan.FromSeconds(5).ToRandom(0.1);
        public RetryDelaySeq RetryDelays { get; init; } = RetryDelaySeq.Exp(1, 5);
    }

    protected Options Settings { get; } = settings;
    protected HostId HostId { get; } = services.GetRequiredService<HostId>();
    protected IDbOperationLogChangeTracker<TDbContext>? OperationLogChangeTracker { get;  }
        = services.GetService<IDbOperationLogChangeTracker<TDbContext>>();
    protected IDbOperationLog<TDbContext> DbOperationLog { get; }
        = services.GetRequiredService<IDbOperationLog<TDbContext>>();

    protected override Task OnRun(DbShard shard, CancellationToken cancellationToken)
    {
        var maxKnownCommitTime = Clocks.SystemClock.Now;
        var batchSize = Settings.BatchSize;
        var lastOperationCount = 0;

        var activitySource = GetType().GetActivitySource();
        var runChain = new AsyncChain($"Read({shard})", async cancellationToken1 => {
            var now = Clocks.SystemClock.Now;

            // Adjusting maxKnownCommitTime to make sure we make progress no matter what
            var minMaxKnownCommitTime = now - Settings.MaxCommitAge;
            if (maxKnownCommitTime < minMaxKnownCommitTime) {
                Log.LogWarning("Read: shifting MaxCommitTime by {Delta}", minMaxKnownCommitTime - maxKnownCommitTime);
                maxKnownCommitTime = minMaxKnownCommitTime;
            }

            // Adjusting batch size
            batchSize = lastOperationCount == batchSize
                ? Math.Min(batchSize << 1, Settings.MaxBatchSize)
                : Settings.BatchSize;

            // Fetching potentially new operations
            var minCommitTime = (maxKnownCommitTime - Settings.MaxCommitDuration).ToDateTime();
            var dbOperations = await DbOperationLog
                .ListNewlyCommitted(shard, minCommitTime, batchSize, cancellationToken1)
                .ConfigureAwait(false);

            // Updating important stuff
            lastOperationCount = dbOperations.Count;
            if (lastOperationCount == 0) {
                maxKnownCommitTime = now;
                return;
            }

            if (lastOperationCount == batchSize)
                Log.LogWarning("Read: fetched {Count}/{BatchSize} operation(s) (full batch), CommitTime >= {MinCommitTime}",
                    lastOperationCount, batchSize, minCommitTime);
            else
                Log.LogDebug("Read: fetched {Count}/{BatchSize} operation(s), CommitTime >= {MinCommitTime}",
                    lastOperationCount, batchSize, minCommitTime);

            var maxCommitTime = dbOperations.Max(o => o.CommitTime).ToMoment();
            maxKnownCommitTime = Moment.Max(maxKnownCommitTime, maxCommitTime);

            // Run completion notifications:
            // Local completions are invoked by TransientOperationScopeProvider
            // _inside_ the command processing pipeline. Trying to trigger them here
            // means a tiny chance of running them _outside_ of command processing
            // pipeline, which makes it possible to see command completing
            // prior to its invalidation logic completion.
            var notifyTasks =
                from dbOperation in dbOperations
                let isLocal = StringComparer.Ordinal.Equals(dbOperation.HostId, HostId.Id.Value)
                where !isLocal
                let operation = dbOperation.ToModel()
                select OperationCompletionNotifier.NotifyCompleted(operation, null);
            await notifyTasks
                .Collect(HardwareInfo.GetProcessorCountFactor(64, 64))
                .ConfigureAwait(false);
        }).Trace(() => activitySource.StartActivity(nameof(Read)).AddShardTags(shard), Log);

        var waitForChangesChain = new AsyncChain("WaitForChanges", async cancellationToken1 => {
            if (lastOperationCount == batchSize) {
                await Clocks.CpuClock.Delay(Settings.MinDelay, cancellationToken1).ConfigureAwait(false);
                return;
            }

            var unconditionalCheckPeriod = Settings.UnconditionalCheckPeriod.Next();
            if (OperationLogChangeTracker == null) {
                var delayTask = Clocks.CpuClock.Delay(unconditionalCheckPeriod, cancellationToken1);
                await delayTask.ConfigureAwait(false);
                return;
            }

            var cts = cancellationToken1.CreateLinkedTokenSource();
            try {
                var notificationTask = OperationLogChangeTracker.WaitForChanges(shard, cts.Token);
                var delayTask = Clocks.CpuClock.Delay(unconditionalCheckPeriod, cts.Token);
                var completedTask = await Task.WhenAny(notificationTask, delayTask).ConfigureAwait(false);
                await completedTask.ConfigureAwait(false);
            }
            finally {
                cts.CancelAndDisposeSilently();
            }
        });

        var chain = runChain
            .RetryForever(Settings.RetryDelays, Clocks.CpuClock, Log)
            .Append(waitForChangesChain)
            .CycleForever()
            .Log(Log);

        return chain.Start(cancellationToken);
    }
}
