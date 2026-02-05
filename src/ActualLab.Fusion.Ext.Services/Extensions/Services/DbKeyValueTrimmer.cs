using ActualLab.Fusion.Diagnostics;
using Microsoft.EntityFrameworkCore;
using ActualLab.Fusion.EntityFramework;

namespace ActualLab.Fusion.Extensions.Services;

/// <summary>
/// A background worker that periodically removes expired key-value entries from the database.
/// </summary>
public class DbKeyValueTrimmer<TDbContext,
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TDbKeyValue> : DbShardWorkerBase<TDbContext>
    where TDbContext : DbContext
    where TDbKeyValue : DbKeyValue, new()
{
    /// <summary>
    /// Configuration options for <see cref="DbKeyValueTrimmer{TDbContext, TDbKeyValue}"/>.
    /// </summary>
    public record Options
    {
        public static Options Default { get; set; } = new();

        public int BatchSize { get; init; } = 100;
        public RandomTimeSpan CheckPeriod { get; init; } = TimeSpan.FromMinutes(15).ToRandom(0.1);
        public RetryDelaySeq RetryDelays { get; init; } = RetryDelaySeq.Exp(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(10));
        public LogLevel LogLevel { get; init; } = LogLevel.Information;
        public bool IsTracingEnabled { get; init; }
    }

    protected Options Settings { get; }
    protected IKeyValueStore KeyValueStore { get; init; }
    protected bool IsLoggingEnabled { get; }

    public DbKeyValueTrimmer(Options settings, IServiceProvider services)
        : base(services)
    {
        Settings = settings;
        IsLoggingEnabled = Log.IsLogging(Settings.LogLevel);
        KeyValueStore = services.GetRequiredService<IKeyValueStore>();
    }

    protected override Task OnRun(string shard, CancellationToken cancellationToken)
    {
        var lastTrimCount = 0;

        var activitySource = FusionInstruments.ActivitySource.IfEnabled(Settings.IsTracingEnabled);
        var runChain = new AsyncChain($"Trim({shard})", async cancellationToken1 => {
            var dbContext = await DbHub.CreateDbContext(shard, true, cancellationToken1).ConfigureAwait(false);
            await using var _ = dbContext.ConfigureAwait(false);
            dbContext.EnableChangeTracking(false);

            lastTrimCount = 0;
            var minExpiresAt = Clocks.SystemClock.Now.ToDateTime();
            var keys = await dbContext.Set<TDbKeyValue>().AsQueryable()
                .Where(o => o.ExpiresAt < minExpiresAt)
                .OrderBy(o => o.ExpiresAt)
                .Take(Settings.BatchSize)
                .Select(o => o.Key)
                .ToArrayAsync(cancellationToken1).ConfigureAwait(false);
            if (keys.Length == 0)
                return;

            // This must be done via IKeyValueStore & operations,
            // otherwise invalidation won't happen for removed entries
            await KeyValueStore.Remove(shard, keys, cancellationToken1).ConfigureAwait(false);
            lastTrimCount = keys.Length;

            if (lastTrimCount > 0 && IsLoggingEnabled)
                Log.Log(Settings.LogLevel,
                    "Trim({Shard}) trimmed {Count} entries", shard, lastTrimCount);
            // ReSharper disable once ExplicitCallerInfoArgument
        }).Trace(() => activitySource.StartActivity(GetType(), "Trim").AddShardTags(shard), Log);

        var chain = runChain
            .RetryForever(Settings.RetryDelays, Clocks.CpuClock, Log)
            .AppendDelay(
                () => lastTrimCount < Settings.BatchSize ? Settings.CheckPeriod : TimeSpan.Zero,
                Clocks.CpuClock)
            .CycleForever()
            .Log(Log);

        return chain.Start(cancellationToken);
    }
}
