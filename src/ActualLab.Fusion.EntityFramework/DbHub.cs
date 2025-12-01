using System.Data;
using Microsoft.EntityFrameworkCore;
using ActualLab.Fusion.EntityFramework.Internal;
using ActualLab.Fusion.EntityFramework.Operations;
using ActualLab.Resilience;
using ActualLab.Versioning;

namespace ActualLab.Fusion.EntityFramework;

public interface IDbHub : IHasServices, IDisposable, IAsyncDisposable
{
    public HostId HostId { get; }
    public IDbShardResolver ShardResolver { get; }
    public IDbShardRegistry ShardRegistry { get; }
    public IShardDbContextFactory ContextFactory { get; }
    public VersionGenerator<long> VersionGenerator { get; }
    public ChaosMaker ChaosMaker { get; }
    public MomentClockSet Clocks { get; }
    public ICommander Commander { get; }

    public ValueTask<DbContext> CreateDbContext(CancellationToken cancellationToken = default);
    public ValueTask<DbContext> CreateDbContext(bool readWrite, CancellationToken cancellationToken = default);
    public ValueTask<DbContext> CreateDbContext(string shard, CancellationToken cancellationToken = default);
    public ValueTask<DbContext> CreateDbContext(string shard, bool readWrite, CancellationToken cancellationToken = default);
    public ValueTask<DbContext> CreateOperationDbContext(CancellationToken cancellationToken = default);
    public ValueTask<DbContext> CreateOperationDbContext(string shard, CancellationToken cancellationToken = default);
    public ValueTask<DbContext> CreateOperationDbContext(IsolationLevel isolationLevel, CancellationToken cancellationToken = default);
    public ValueTask<DbContext> CreateOperationDbContext(string shard, IsolationLevel isolationLevel, CancellationToken cancellationToken = default);
}

public class DbHub<TDbContext>(IServiceProvider services) : IDbHub
    where TDbContext : DbContext
{
#if NET9_0_OR_GREATER
    private readonly Lock _lock = new();
#else
    private readonly object _lock = new();
#endif
    private TDbContext? _templateDbContext;

    protected ILogger Log => field ??= Services.LogFor(GetType());

    protected TDbContext TemplateDbContext {
        get {
            if (_templateDbContext is { } value)
                return value;

            lock (_lock) {
                return _templateDbContext ??=
                    ContextFactory.CreateDbContext(ShardRegistry.HasSingleShard ? DbShard.Single : DbShard.Template);
            }
        }
    }

    public IServiceProvider Services { get; } = services;
    public HostId HostId => field ??= Commander.Hub.HostId;
    public IDbShardResolver<TDbContext> ShardResolver => field ??= Services.DbShardResolver<TDbContext>();
    public IDbShardRegistry<TDbContext> ShardRegistry => ShardResolver.ShardRegistry;
    public IShardDbContextFactory<TDbContext> ContextFactory
        => field ??= Services.GetRequiredService<IShardDbContextFactory<TDbContext>>();
    public VersionGenerator<long> VersionGenerator
        => field ??= Commander.Hub.VersionGenerator;
    public ChaosMaker ChaosMaker
        => field ??= Commander.Hub.ChaosMaker;
    public MomentClockSet Clocks
        => field ??= Services.Clocks();
    public ICommander Commander
        => field ??= Services.Commander();

    public void Dispose()
        => _templateDbContext?.Dispose();

    public ValueTask DisposeAsync()
        => _templateDbContext?.DisposeAsync() ?? default;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<TDbContext> CreateDbContext(CancellationToken cancellationToken = default)
        => CreateDbContext(DbShard.Single, false, cancellationToken);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<TDbContext> CreateDbContext(bool readWrite, CancellationToken cancellationToken = default)
        => CreateDbContext(DbShard.Single, readWrite, cancellationToken);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<TDbContext> CreateDbContext(string shard, CancellationToken cancellationToken = default)
        => CreateDbContext(shard, false, cancellationToken);

    public ValueTask<TDbContext> CreateDbContext(string shard, bool readWrite, CancellationToken cancellationToken = default)
    {
        ExecutionStrategyExt.Suspend(TemplateDbContext); // This call sets AsyncLocal for the caller, so it has to go first
        return CreateDbContextImpl(shard, readWrite, cancellationToken);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<TDbContext> CreateOperationDbContext(CancellationToken cancellationToken = default)
        => CreateOperationDbContext(DbShard.Single, IsolationLevel.Unspecified, cancellationToken);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<TDbContext> CreateOperationDbContext(string shard, CancellationToken cancellationToken = default)
        => CreateOperationDbContext(shard, IsolationLevel.Unspecified, cancellationToken);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<TDbContext> CreateOperationDbContext(
        IsolationLevel isolationLevel,
        CancellationToken cancellationToken = default)
        => CreateOperationDbContext(DbShard.Single, isolationLevel, cancellationToken);

    public ValueTask<TDbContext> CreateOperationDbContext(
        string shard,
        IsolationLevel isolationLevel,
        CancellationToken cancellationToken = default)
    {
        ExecutionStrategyExt.Suspend(TemplateDbContext); // This call sets AsyncLocal for the caller, so it has to go first
        return CreateOperationDbContextImpl(shard, isolationLevel, cancellationToken);
    }

    // Explicit interface implementations

    IDbShardRegistry IDbHub.ShardRegistry => ShardRegistry;
    IDbShardResolver IDbHub.ShardResolver => ShardResolver;
    IShardDbContextFactory IDbHub.ContextFactory => ContextFactory;

    async ValueTask<DbContext> IDbHub.CreateDbContext(CancellationToken cancellationToken)
        => await CreateDbContext(cancellationToken).ConfigureAwait(false);
    async ValueTask<DbContext> IDbHub.CreateDbContext(bool readWrite, CancellationToken cancellationToken)
        => await CreateDbContext(readWrite, cancellationToken).ConfigureAwait(false);
    async ValueTask<DbContext> IDbHub.CreateDbContext(string shard, CancellationToken cancellationToken)
        => await CreateDbContext(shard, cancellationToken).ConfigureAwait(false);
    async ValueTask<DbContext> IDbHub.CreateDbContext(string shard, bool readWrite, CancellationToken cancellationToken)
        => await CreateDbContext(shard, readWrite, cancellationToken).ConfigureAwait(false);

    async ValueTask<DbContext> IDbHub.CreateOperationDbContext(CancellationToken cancellationToken)
        => await CreateOperationDbContext(cancellationToken).ConfigureAwait(false);
    async ValueTask<DbContext> IDbHub.CreateOperationDbContext(string shard, CancellationToken cancellationToken)
        => await CreateOperationDbContext(shard, cancellationToken).ConfigureAwait(false);
    async ValueTask<DbContext> IDbHub.CreateOperationDbContext(IsolationLevel isolationLevel, CancellationToken cancellationToken)
        => await CreateOperationDbContext(isolationLevel, cancellationToken).ConfigureAwait(false);
    async ValueTask<DbContext> IDbHub.CreateOperationDbContext(string shard, IsolationLevel isolationLevel, CancellationToken cancellationToken)
        => await CreateOperationDbContext(shard, isolationLevel, cancellationToken).ConfigureAwait(false);

    // Protected methods

    protected async ValueTask<TDbContext> CreateDbContextImpl(
        string shard,
        bool readWrite,
        CancellationToken cancellationToken)
    {
        var dbContext = await ContextFactory.CreateDbContextAsync(shard, cancellationToken).ConfigureAwait(false);
        dbContext.ReadWrite(readWrite);
        return dbContext;
    }

    protected async ValueTask<TDbContext> CreateOperationDbContextImpl(
        string shard,
        IsolationLevel isolationLevel,
        CancellationToken cancellationToken)
    {
        var operationScope = DbOperationScope<TDbContext>.GetOrCreate(CommandContext.GetCurrent(), isolationLevel);
        var dbContext = await CreateDbContextImpl(shard, readWrite: true, cancellationToken).ConfigureAwait(false);
        await operationScope.InitializeDbContext(dbContext, shard, cancellationToken).ConfigureAwait(false);
        return dbContext;
    }
}
