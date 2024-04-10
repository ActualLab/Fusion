using System.Data;
using Microsoft.EntityFrameworkCore;
using ActualLab.Fusion.EntityFramework.Internal;
using ActualLab.Resilience;
using ActualLab.Versioning;

namespace ActualLab.Fusion.EntityFramework;

public class DbHub<TDbContext>(IServiceProvider services)
    where TDbContext : DbContext
{
    private HostId? _hostId;
    private IDbShardRegistry<TDbContext>? _shardRegistry;
    private IShardDbContextFactory<TDbContext>? _contextFactory;
    private VersionGenerator<long>? _versionGenerator;
    private ChaosMaker? _chaosMaker;
    private MomentClockSet? _clocks;
    private ICommander? _commander;
    private ILogger? _log;

    protected ILogger Log => _log ??= Services.LogFor(GetType());
    protected IServiceProvider Services { get; } = services;

    public HostId HostId => _hostId ??= Commander.Hub.HostId;
    public IDbShardRegistry<TDbContext> ShardRegistry
        => _shardRegistry ??= Services.GetRequiredService<IDbShardRegistry<TDbContext>>();
    public IShardDbContextFactory<TDbContext> ContextFactory
        => _contextFactory ??= Services.GetRequiredService<IShardDbContextFactory<TDbContext>>();
    public VersionGenerator<long> VersionGenerator
        => _versionGenerator ??= Commander.Hub.VersionGenerator;

    public IsolationLevel CommandIsolationLevel {
        get {
            var context = CommandContext.GetCurrent();
            var operationScope = context.Items.Get<DbOperationScope<TDbContext>>().Require();
            return operationScope.IsolationLevel;
        }
        set {
            var context = CommandContext.GetCurrent();
            var operationScope = context.Items.Get<DbOperationScope<TDbContext>>().Require();
            operationScope.IsolationLevel = value;
        }
    }

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
        => CreateCommandDbContext(default, cancellationToken);

    public async ValueTask<TDbContext> CreateCommandDbContext(DbShard shard, CancellationToken cancellationToken = default)
    {
        if (Computed.IsInvalidating())
            throw Errors.CreateCommandDbContextIsCalledFromInvalidationCode();

        var commandContext = CommandContext.GetCurrent();
        var operationScope = commandContext.Items.Get<DbOperationScope<TDbContext>>().Require();
        var dbContext = await CreateDbContext(shard, readWrite: true, cancellationToken).ConfigureAwait(false);
        await operationScope.InitializeDbContext(dbContext, shard, cancellationToken).ConfigureAwait(false);
        return dbContext;
    }
}
