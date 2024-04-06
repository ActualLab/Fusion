using Microsoft.EntityFrameworkCore;
using ActualLab.Fusion.EntityFramework;

namespace ActualLab.Fusion.Authentication.Services;

public abstract class DbSessionInfoTrimmer<TDbContext> : DbShardWorkerBase<TDbContext>
    where TDbContext : DbContext
{
    public record Options
    {
        public RandomTimeSpan CheckPeriod { get; init; } = TimeSpan.FromMinutes(10).ToRandom(0.1);
        public RandomTimeSpan NextBatchDelay { get; init; } = TimeSpan.FromSeconds(0.1).ToRandom(0.25);
        public RetryDelaySeq RetryDelays { get; init; } = RetryDelaySeq.Exp(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(10));
        public TimeSpan MaxSessionAge { get; init; } = TimeSpan.FromDays(60);
        public int BatchSize { get; init; } = 256;
        public LogLevel LogLevel { get; init; } = LogLevel.Information;
    }

    protected Options Settings { get; }
    protected bool IsLoggingEnabled { get; }

    protected DbSessionInfoTrimmer(Options settings, IServiceProvider services)
        : base(services)
    {
        Settings = settings;
        IsLoggingEnabled = Log.IsLogging(Settings.LogLevel);
    }
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

    protected override Task OnRun(DbShard shard, CancellationToken cancellationToken)
    {
        var lastTrimCount = 0;

        var activitySource = GetType().GetActivitySource();
        var runChain = new AsyncChain($"Trim({shard})", async cancellationToken1 => {
            var maxLastSeenAt = (Clocks.SystemClock.Now - Settings.MaxSessionAge).ToDateTime();
            lastTrimCount = await Sessions
                .Trim(shard, maxLastSeenAt, Settings.BatchSize, cancellationToken1)
                .ConfigureAwait(false);

            if (lastTrimCount > 0 && IsLoggingEnabled)
                Log.Log(Settings.LogLevel,
                    "Trim({Shard}) trimmed {Count} sessions", shard.Value, lastTrimCount);
            // ReSharper disable once ExplicitCallerInfoArgument
        }).Trace(() => activitySource.StartActivity("Trim").AddShardTags(shard), Log);

        var chain = runChain
            .RetryForever(Settings.RetryDelays, Clocks.CpuClock, Log)
            .AppendDelay(
                () => lastTrimCount < Settings.BatchSize ? Settings.CheckPeriod : Settings.NextBatchDelay,
                Clocks.CpuClock)
            .CycleForever()
            .Log(Log);

        return chain.Start(cancellationToken);
    }
}
