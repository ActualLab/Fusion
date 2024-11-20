using System.Data;
using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using ActualLab.Fusion.EntityFramework.Internal;
using ActualLab.Fusion.EntityFramework.Operations;
using ActualLab.Resilience;
using ActualLab.Versioning;

namespace ActualLab.Fusion.EntityFramework;

public interface IDbHub : IHasServices
{
    public HostId HostId { get; }
    public IDbShardResolver ShardResolver { get; }
    public IDbShardRegistry ShardRegistry { get; }
    public IShardDbContextFactory ContextFactory { get; }
    public VersionGenerator<long> VersionGenerator { get; }
    public ChaosMaker ChaosMaker { get; }
    public MomentClockSet Clocks { get; }
    public ICommander Commander { get; }

    ValueTask<DbContext> CreateDbContext(CancellationToken cancellationToken = default);
    ValueTask<DbContext> CreateDbContext(bool readWrite, CancellationToken cancellationToken = default);
    ValueTask<DbContext> CreateDbContext(DbShard shard, CancellationToken cancellationToken = default);
    ValueTask<DbContext> CreateDbContext(DbShard shard, bool readWrite, CancellationToken cancellationToken = default);
    ValueTask<DbContext> CreateOperationDbContext(CancellationToken cancellationToken = default);
    ValueTask<DbContext> CreateOperationDbContext(DbShard shard, CancellationToken cancellationToken = default);
    ValueTask<DbContext> CreateOperationDbContext(IsolationLevel isolationLevel, CancellationToken cancellationToken = default);
    ValueTask<DbContext> CreateOperationDbContext(DbShard shard, IsolationLevel isolationLevel, CancellationToken cancellationToken = default);
}

public class DbHub<TDbContext>(IServiceProvider services) : IDbHub
    where TDbContext : DbContext
{
    [field: AllowNull, MaybeNull]
    protected ILogger Log => field ??= Services.LogFor(GetType());

    public IServiceProvider Services { get; } = services;
    [field: AllowNull, MaybeNull]
    public HostId HostId => field ??= Commander.Hub.HostId;
    [field: AllowNull, MaybeNull]
    public IDbShardResolver<TDbContext> ShardResolver => field ??= Services.DbShardResolver<TDbContext>();
    public IDbShardRegistry<TDbContext> ShardRegistry => ShardResolver.ShardRegistry;
    [field: AllowNull, MaybeNull]
    public IShardDbContextFactory<TDbContext> ContextFactory
        => field ??= Services.GetRequiredService<IShardDbContextFactory<TDbContext>>();
    [field: AllowNull, MaybeNull]
    public VersionGenerator<long> VersionGenerator
        => field ??= Commander.Hub.VersionGenerator;
    [field: AllowNull, MaybeNull]
    public ChaosMaker ChaosMaker
        => field ??= Commander.Hub.ChaosMaker;
    [field: AllowNull, MaybeNull]
    public MomentClockSet Clocks
        => field ??= Services.Clocks();
    [field: AllowNull, MaybeNull]
    public ICommander Commander
        => field ??= Services.Commander();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<TDbContext> CreateDbContext(CancellationToken cancellationToken = default)
        => CreateDbContext(default, false, cancellationToken);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<TDbContext> CreateDbContext(bool readWrite, CancellationToken cancellationToken = default)
        => CreateDbContext(default, readWrite, cancellationToken);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<TDbContext> CreateDbContext(DbShard shard, CancellationToken cancellationToken = default)
        => CreateDbContext(shard, false, cancellationToken);

    public async ValueTask<TDbContext> CreateDbContext(DbShard shard, bool readWrite, CancellationToken cancellationToken = default)
    {
        var dbContext = await ContextFactory.CreateDbContextAsync(shard, cancellationToken).ConfigureAwait(false);
        dbContext.SuppressExecutionStrategy().ReadWrite(readWrite);
        return dbContext;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<TDbContext> CreateOperationDbContext(CancellationToken cancellationToken = default)
        => CreateOperationDbContext(default, IsolationLevel.Unspecified, cancellationToken);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<TDbContext> CreateOperationDbContext(DbShard shard, CancellationToken cancellationToken = default)
        => CreateOperationDbContext(shard, IsolationLevel.Unspecified, cancellationToken);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<TDbContext> CreateOperationDbContext(
        IsolationLevel isolationLevel,
        CancellationToken cancellationToken = default)
        => CreateOperationDbContext(default, isolationLevel, cancellationToken);

    public async ValueTask<TDbContext> CreateOperationDbContext(
        DbShard shard,
        IsolationLevel isolationLevel,
        CancellationToken cancellationToken = default)
    {
        var operationScope = DbOperationScope<TDbContext>.GetOrCreate(CommandContext.GetCurrent(), isolationLevel);
        var dbContext = await CreateDbContext(shard, readWrite: true, cancellationToken).ConfigureAwait(false);
        await operationScope.InitializeDbContext(dbContext, shard, cancellationToken).ConfigureAwait(false);
        return dbContext;
    }

    // Explicit interface implementations

    IDbShardRegistry IDbHub.ShardRegistry => ShardRegistry;
    IDbShardResolver IDbHub.ShardResolver => ShardResolver;
    IShardDbContextFactory IDbHub.ContextFactory => ContextFactory;

    async ValueTask<DbContext> IDbHub.CreateDbContext(CancellationToken cancellationToken)
        => await CreateDbContext(cancellationToken).ConfigureAwait(false);
    async ValueTask<DbContext> IDbHub.CreateDbContext(bool readWrite, CancellationToken cancellationToken)
        => await CreateDbContext(readWrite, cancellationToken).ConfigureAwait(false);
    async ValueTask<DbContext> IDbHub.CreateDbContext(DbShard shard, CancellationToken cancellationToken)
        => await CreateDbContext(shard, cancellationToken).ConfigureAwait(false);
    async ValueTask<DbContext> IDbHub.CreateDbContext(DbShard shard, bool readWrite, CancellationToken cancellationToken)
        => await CreateDbContext(shard, readWrite, cancellationToken).ConfigureAwait(false);

    async ValueTask<DbContext> IDbHub.CreateOperationDbContext(CancellationToken cancellationToken)
        => await CreateOperationDbContext(cancellationToken).ConfigureAwait(false);
    async ValueTask<DbContext> IDbHub.CreateOperationDbContext(DbShard shard, CancellationToken cancellationToken)
        => await CreateOperationDbContext(shard, cancellationToken).ConfigureAwait(false);
    async ValueTask<DbContext> IDbHub.CreateOperationDbContext(IsolationLevel isolationLevel, CancellationToken cancellationToken)
        => await CreateOperationDbContext(isolationLevel, cancellationToken).ConfigureAwait(false);
    async ValueTask<DbContext> IDbHub.CreateOperationDbContext(DbShard shard, IsolationLevel isolationLevel, CancellationToken cancellationToken)
        => await CreateOperationDbContext(shard, isolationLevel, cancellationToken).ConfigureAwait(false);
}
