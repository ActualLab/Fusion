using Microsoft.EntityFrameworkCore;
using ActualLab.Versioning;

namespace ActualLab.Fusion.EntityFramework;

public abstract class DbShardWorkerBase<TDbContext>(
    IServiceProvider services,
    CancellationTokenSource? stopTokenSource = null
    ) : WorkerBase(stopTokenSource)
    where TDbContext : DbContext
{
    private ILogger? _log;
    private DbHub<TDbContext>? _dbHub;

    protected IServiceProvider Services { get; init; } = services;
    protected DbHub<TDbContext> DbHub => _dbHub ??= Services.DbHub<TDbContext>();
    protected VersionGenerator<long> VersionGenerator => DbHub.VersionGenerator;
    protected MomentClockSet Clocks => DbHub.Clocks;
    protected ICommander Commander => DbHub.Commander;
    protected ILogger Log => _log ??= Services.LogFor(GetType());

    protected virtual IState<ImmutableHashSet<DbShard>> WorkerShards => DbHub.ShardRegistry.UsedShards;

    protected override Task OnRun(CancellationToken cancellationToken)
    {
        var changes = WorkerShards
            .Changes(cancellationToken)
            .SkipSyncItems(cancellationToken)
            .Select(x => x.Value.Remove(DbShard.Template));
        return changes.RunItemTasks(OnRun, cancellationToken);
    }

    protected abstract Task OnRun(DbShard shard, CancellationToken cancellationToken);
}
