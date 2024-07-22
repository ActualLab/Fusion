using System.Data;
using Microsoft.EntityFrameworkCore;
using ActualLab.Fusion.EntityFramework.Internal;
using ActualLab.Fusion.EntityFramework.Operations;
using ActualLab.Resilience;
using ActualLab.Versioning;

namespace ActualLab.Fusion.EntityFramework;

public interface IDbHub : IHasServices
{
    HostId HostId { get; }
    IDbShardResolver ShardResolver { get; }
    IDbShardRegistry ShardRegistry { get; }
    IShardDbContextFactory ContextFactory { get; }
    VersionGenerator<long> VersionGenerator { get; }
    ChaosMaker ChaosMaker { get; }
    MomentClockSet Clocks { get; }
    ICommander Commander { get; }

    ValueTask<DbContext> CreateDbContext(CancellationToken cancellationToken = default);
    ValueTask<DbContext> CreateDbContext(bool readWrite, CancellationToken cancellationToken = default);
    ValueTask<DbContext> CreateDbContext(DbShard shard, CancellationToken cancellationToken = default);
    ValueTask<DbContext> CreateDbContext(DbShard shard, bool readWrite, CancellationToken cancellationToken = default);
    ValueTask<DbContext> CreateCommandDbContext(CancellationToken cancellationToken = default);
    ValueTask<DbContext> CreateCommandDbContext(DbShard shard, CancellationToken cancellationToken = default);
    ValueTask<DbContext> CreateCommandDbContext(IsolationLevel isolationLevel, CancellationToken cancellationToken = default);
    ValueTask<DbContext> CreateCommandDbContext(DbShard shard, IsolationLevel isolationLevel, CancellationToken cancellationToken = default);
}

public class DbHub<TDbContext>(IServiceProvider services) : IDbHub
    where TDbContext : DbContext
{
    private HostId? _hostId;
    private IDbShardResolver<TDbContext>? _shardResolver;
    private IShardDbContextFactory<TDbContext>? _contextFactory;
    private VersionGenerator<long>? _versionGenerator;
    private ChaosMaker? _chaosMaker;
    private MomentClockSet? _clocks;
    private ICommander? _commander;
    private ILogger? _log;

    protected ILogger Log => _log ??= Services.LogFor(GetType());

    public IServiceProvider Services { get; } = services;
    public HostId HostId => _hostId ??= Commander.Hub.HostId;
    public IDbShardResolver<TDbContext> ShardResolver => _shardResolver ??= Services.DbShardResolver<TDbContext>();
    public IDbShardRegistry<TDbContext> ShardRegistry => ShardResolver.ShardRegistry;
    public IShardDbContextFactory<TDbContext> ContextFactory
        => _contextFactory ??= Services.GetRequiredService<IShardDbContextFactory<TDbContext>>();
    public VersionGenerator<long> VersionGenerator
        => _versionGenerator ??= Commander.Hub.VersionGenerator;

    public ChaosMaker ChaosMaker
        => _chaosMaker ??= Commander.Hub.ChaosMaker;
    public MomentClockSet Clocks
        => _clocks ??= Services.Clocks();
    public ICommander Commander
        => _commander ??= Services.Commander();

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
    public ValueTask<TDbContext> CreateCommandDbContext(CancellationToken cancellationToken = default)
        => CreateCommandDbContext(default, IsolationLevel.Unspecified, cancellationToken);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<TDbContext> CreateCommandDbContext(DbShard shard, CancellationToken cancellationToken = default)
        => CreateCommandDbContext(shard, IsolationLevel.Unspecified, cancellationToken);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<TDbContext> CreateCommandDbContext(
        IsolationLevel isolationLevel,
        CancellationToken cancellationToken = default)
        => CreateCommandDbContext(default, isolationLevel, cancellationToken);

    public async ValueTask<TDbContext> CreateCommandDbContext(
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

    async ValueTask<DbContext> IDbHub.CreateCommandDbContext(CancellationToken cancellationToken)
        => await CreateCommandDbContext(cancellationToken).ConfigureAwait(false);
    async ValueTask<DbContext> IDbHub.CreateCommandDbContext(DbShard shard, CancellationToken cancellationToken)
        => await CreateCommandDbContext(shard, cancellationToken).ConfigureAwait(false);
    async ValueTask<DbContext> IDbHub.CreateCommandDbContext(IsolationLevel isolationLevel, CancellationToken cancellationToken)
        => await CreateCommandDbContext(isolationLevel, cancellationToken).ConfigureAwait(false);
    async ValueTask<DbContext> IDbHub.CreateCommandDbContext(DbShard shard, IsolationLevel isolationLevel, CancellationToken cancellationToken)
        => await CreateCommandDbContext(shard, isolationLevel, cancellationToken).ConfigureAwait(false);
}
