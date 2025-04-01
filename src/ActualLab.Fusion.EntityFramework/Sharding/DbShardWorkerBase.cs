using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using ActualLab.Versioning;

namespace ActualLab.Fusion.EntityFramework;

public abstract class DbShardWorkerBase<TDbContext>(
    IServiceProvider services,
    CancellationTokenSource? stopTokenSource = null
    ) : WorkerBase(stopTokenSource)
    where TDbContext : DbContext
{
    protected IServiceProvider Services { get; init; } = services;
    [field: AllowNull, MaybeNull]
    protected DbHub<TDbContext> DbHub => field ??= Services.DbHub<TDbContext>();
    protected VersionGenerator<long> VersionGenerator => DbHub.VersionGenerator;
    protected MomentClockSet Clocks => DbHub.Clocks;
    protected ICommander Commander => DbHub.Commander;
    [field: AllowNull, MaybeNull]
    protected ILogger Log => field ??= Services.LogFor(GetType());

    protected virtual State<ImmutableHashSet<DbShard>> WorkerShards => DbHub.ShardRegistry.UsedShards;

    protected override Task OnRun(CancellationToken cancellationToken)
    {
        var changes = WorkerShards
            .Changes(cancellationToken)
            .SkipSyncItems(cancellationToken)
            .Select(x => x.Value.Remove(DbShard.Template));
        return changes.RunItemTasks(
            OnRun,
            // (shards, _, _) => Log.LogWarning("New shards: {Shards}", shards.ToDelimitedString()),
            cancellationToken);
    }

    protected abstract Task OnRun(DbShard shard, CancellationToken cancellationToken);
}
