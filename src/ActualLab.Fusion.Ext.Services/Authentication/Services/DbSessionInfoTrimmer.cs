using Microsoft.EntityFrameworkCore;
using ActualLab.Fusion.EntityFramework;

namespace ActualLab.Fusion.Authentication.Services;

public abstract class DbSessionInfoTrimmer<TDbContext>(
    DbSessionInfoTrimmer<TDbContext>.Options settings,
    IServiceProvider services)
    : DbShardWorkerBase<TDbContext>(services)
    where TDbContext : DbContext
{
    public record Options
    {
        public TimeSpan MaxSessionAge { get; init; } = TimeSpan.FromDays(60);
        public int BatchSize { get; init; } = 1024;
        public RandomTimeSpan CheckPeriod { get; init; } =  TimeSpan.FromMinutes(10).ToRandom(0.25);
        public RandomTimeSpan InterBatchDelay { get; init; } = TimeSpan.FromSeconds(1).ToRandom(0.25);
        public RetryDelaySeq RetryDelays { get; init; } = RetryDelaySeq.Exp(TimeSpan.FromSeconds(15), TimeSpan.FromMinutes(10));
        public LogLevel LogLevel { get; init; } = LogLevel.Information;
    }

    protected Options Settings { get; } = settings;
    protected ILogger? DefaultLog => Log.IfEnabled(Settings.LogLevel);
}

public class DbSessionInfoTrimmer<TDbContext, TDbSessionInfo, TDbUserId>(
    DbSessionInfoTrimmer<TDbContext>.Options settings,
    IServiceProvider services
    ) : DbSessionInfoTrimmer<TDbContext>(settings, services)
    where TDbContext : DbContext
    where TDbSessionInfo : DbSessionInfo<TDbUserId>, new()
    where TDbUserId : notnull
{
    protected IDbSessionInfoRepo<TDbContext, TDbSessionInfo, TDbUserId> Sessions { get; }
        = services.GetRequiredService<IDbSessionInfoRepo<TDbContext, TDbSessionInfo, TDbUserId>>();
    protected IMomentClock SystemClock => Clocks.SystemClock;

    protected override Task OnRun(DbShard shard, CancellationToken cancellationToken)
        => new AsyncChain($"Trim({shard})", ct => Trim(shard, ct))
            .RetryForever(Settings.RetryDelays, SystemClock, Log)
            .CycleForever()
            .Log(Log)
            .PrependDelay(Settings.InterBatchDelay, SystemClock)
            .Start(cancellationToken);

    protected virtual async Task Trim(DbShard shard, CancellationToken cancellationToken)
    {
        var batchSize = Settings.BatchSize;
        while (true) {
            var maxLastSeenAt = (SystemClock.Now - Settings.MaxSessionAge).ToDateTime();
            using (var _ = ActivitySource.StartActivity().AddShardTags(shard)) {
                var count = await Sessions.Trim(shard, maxLastSeenAt, batchSize, cancellationToken)
                    .ConfigureAwait(false);
                if (count > 0)
                    DefaultLog?.Log(Settings.LogLevel, "Trim({Shard}) trimmed {Count} sessions", shard.Value, count);
                if (count != batchSize)
                    break;
            }
            await SystemClock.Delay(Settings.InterBatchDelay.Next(), cancellationToken).ConfigureAwait(false);
        }
        await SystemClock.Delay(Settings.CheckPeriod.Next(), cancellationToken).ConfigureAwait(false);
    }
}
