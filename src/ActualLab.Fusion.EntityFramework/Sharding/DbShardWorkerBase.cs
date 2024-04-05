using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using ActualLab.Versioning;

namespace ActualLab.Fusion.EntityFramework;

public abstract class DbShardWorkerBase<
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TDbContext>(
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

    protected ValueTask<TDbContext> CreateDbContext(CancellationToken cancellationToken = default)
        => DbHub.CreateDbContext(cancellationToken);
    protected ValueTask<TDbContext> CreateDbContext(bool readWrite, CancellationToken cancellationToken = default)
        => DbHub.CreateDbContext(readWrite, cancellationToken);
    protected ValueTask<TDbContext> CreateDbContext(DbShard shard, CancellationToken cancellationToken = default)
        => DbHub.CreateDbContext(shard, cancellationToken);
    protected ValueTask<TDbContext> CreateDbContext(DbShard shard, bool readWrite, CancellationToken cancellationToken = default)
        => DbHub.CreateDbContext(shard, readWrite, cancellationToken);

    protected ValueTask<TDbContext> CreateCommandDbContext(CancellationToken cancellationToken = default)
        => DbHub.CreateCommandDbContext(cancellationToken);
    protected ValueTask<TDbContext> CreateCommandDbContext(DbShard shard, CancellationToken cancellationToken = default)
        => DbHub.CreateCommandDbContext(shard, cancellationToken);

    protected override Task OnRun(CancellationToken cancellationToken)
    {
        var usedShards = DbHub.ShardRegistry.UsedShards;
        var changes = usedShards
            .Changes(FixedDelayer.ZeroUnsafe, cancellationToken)
            .SkipSyncItems(cancellationToken)
            .Select(x => x.Value.Remove(DbShard.Template));
        return changes.RunItemTasks(OnRun, cancellationToken);
    }

    protected abstract Task OnRun(DbShard shard, CancellationToken cancellationToken);
}
