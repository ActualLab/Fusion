using System.Data;
using Microsoft.EntityFrameworkCore;
using ActualLab.Fusion.EntityFramework.Internal;
using ActualLab.Versioning;

namespace ActualLab.Fusion.EntityFramework;

public class DbHub<TDbContext>(IServiceProvider services)
    where TDbContext : DbContext
{
    private IDbShardRegistry<TDbContext>? _shardRegistry;
    private IShardDbContextFactory<TDbContext>? _contextFactory;
    private VersionGenerator<long>? _versionGenerator;
    private MomentClockSet? _clocks;
    private ICommander? _commander;
    private ILogger? _log;

    protected ILogger Log => _log ??= Services.LogFor(GetType());
    protected IServiceProvider Services { get; } = services;

    public IDbShardRegistry<TDbContext> ShardRegistry
        => _shardRegistry ??= Services.GetRequiredService<IDbShardRegistry<TDbContext>>();
    public IShardDbContextFactory<TDbContext> ContextFactory
        => _contextFactory ??= Services.GetRequiredService<IShardDbContextFactory<TDbContext>>();
    public VersionGenerator<long> VersionGenerator
        => _versionGenerator ??= Services.VersionGenerator<long>();

    public IsolationLevel CommandIsolationLevel {
        get {
            var commandContext = CommandContext.GetCurrent();
            var operationScope = commandContext.Items.Get<DbOperationScope<TDbContext>>().Require();
            return operationScope.IsolationLevel;
        }
        set {
            var commandContext = CommandContext.GetCurrent();
            var operationScope = commandContext.Items.Get<DbOperationScope<TDbContext>>().Require();
            operationScope.IsolationLevel = value;
        }
    }

    public ICommander Commander
        => _commander ??= Services.Commander();
    public MomentClockSet Clocks
        => _clocks ??= Services.Clocks();

    public ValueTask<TDbContext> CreateDbContext(CancellationToken cancellationToken = default)
        => CreateDbContext(default, false, cancellationToken);
    public ValueTask<TDbContext> CreateDbContext(bool readWrite, CancellationToken cancellationToken = default)
        => CreateDbContext(default, readWrite, cancellationToken);
    public ValueTask<TDbContext> CreateDbContext(DbShard shard, CancellationToken cancellationToken = default)
        => CreateDbContext(shard, false, cancellationToken);
    public async ValueTask<TDbContext> CreateDbContext(DbShard shard, bool readWrite, CancellationToken cancellationToken = default)
    {
        var dbContext = await ContextFactory.CreateDbContextAsync(shard, cancellationToken).ConfigureAwait(false);
        dbContext.SuppressExecutionStrategy().ReadWrite(readWrite);
        return dbContext;
    }

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
