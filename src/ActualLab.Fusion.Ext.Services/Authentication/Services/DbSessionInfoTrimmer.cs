using ActualLab.Fusion.Diagnostics;
using Microsoft.EntityFrameworkCore;
using ActualLab.Fusion.EntityFramework;

namespace ActualLab.Fusion.Authentication.Services;

/// <summary>
/// Abstract base class for a background worker that trims expired session records.
/// </summary>
public abstract class DbSessionInfoTrimmer<TDbContext>(
    DbSessionInfoTrimmer<TDbContext>.Options settings,
    IServiceProvider services)
    : DbShardWorkerBase<TDbContext>(services)
    where TDbContext : DbContext
{
    /// <summary>
    /// Configuration options for <see cref="DbSessionInfoTrimmer{TDbContext}"/>.
    /// </summary>
    public record Options
    {
#if NET7_0_OR_GREATER
        public int BatchSize { get; init; } = 4096; // .NET 7+ uses ExecuteDeleteAsync
#else
        public int BatchSize { get; init; } = 1024; // .NET 6- deletes rows one-by-one
#endif
        public TimeSpan MaxSessionAge { get; init; } = TimeSpan.FromDays(60);
        public RandomTimeSpan CheckPeriod { get; init; } =  TimeSpan.FromMinutes(15).ToRandom(0.25);
        public RetryDelaySeq RetryDelays { get; init; } = RetryDelaySeq.Exp(TimeSpan.FromSeconds(15), TimeSpan.FromMinutes(10));
        public LogLevel LogLevel { get; init; } = LogLevel.Information;
        public bool IsTracingEnabled { get; init; }
    }

    protected Options Settings { get; } = settings;
    protected ILogger? DefaultLog => Log.IfEnabled(Settings.LogLevel);
}

/// <summary>
/// Concrete implementation of <see cref="DbSessionInfoTrimmer{TDbContext}"/> that
/// periodically removes sessions older than a configured threshold.
/// </summary>
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
    protected MomentClock SystemClock => Clocks.SystemClock;

    protected override Task OnRun(string shard, CancellationToken cancellationToken)
        => new AsyncChain($"Trim({shard})", ct => Trim(shard, ct))
            .RetryForever(Settings.RetryDelays, SystemClock, Log)
            .CycleForever()
            .Log(Log)
            .PrependDelay(Settings.CheckPeriod.Next().MultiplyBy(0.1), SystemClock)
            .Start(cancellationToken);

    protected virtual async Task Trim(string shard, CancellationToken cancellationToken)
    {
        var batchSize = Settings.BatchSize;
        while (true) {
            var maxLastSeenAt = (SystemClock.Now - Settings.MaxSessionAge).ToDateTime();
            var activity = FusionInstruments.ActivitySource
                .IfEnabled(Settings.IsTracingEnabled)
                .StartActivity(GetType())
                .AddShardTags(shard);
            try {
                var count = await Sessions.Trim(shard, maxLastSeenAt, batchSize, cancellationToken)
                    .ConfigureAwait(false);
                if (count > 0)
                    DefaultLog?.Log(Settings.LogLevel, "Trim({Shard}) trimmed {Count} sessions", shard, count);
                if (count < batchSize)
                    break;
            }
            catch (Exception e) {
                activity?.Finalize(e, cancellationToken);
                throw;
            }
            finally {
                activity?.Dispose();
            }
        }
        await SystemClock.Delay(Settings.CheckPeriod.Next(), cancellationToken).ConfigureAwait(false);
    }
}
