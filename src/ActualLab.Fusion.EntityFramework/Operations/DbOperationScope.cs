using System.Data;
using System.Data.Common;
using ActualLab.CommandR.Operations;
using ActualLab.Fusion.EntityFramework.Internal;
using ActualLab.Fusion.EntityFramework.LogProcessing;
using ActualLab.Locking;
using ActualLab.Resilience;
using ActualLab.Versioning;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ActualLab.Fusion.EntityFramework.Operations;

public abstract class DbOperationScope : IOperationScope
{
    public record Options
    {
        public static IsolationLevel DefaultIsolationLevel { get; set; } = IsolationLevel.Unspecified;

        public IsolationLevel IsolationLevel { get; init; } = DefaultIsolationLevel;
    }

    public IsolationLevel IsolationLevel { get; protected init; }
    public CommandContext CommandContext { get; protected init; } = null!;
    protected IServiceProvider Services => CommandContext.Services;
    public Operation Operation { get; protected init; } = null!;
    public DbShard Shard { get; protected set; }
    public bool IsTransient => false;
    public bool IsUsed => MasterDbContext != null;
    public bool? IsCommitted { get; protected set; }
    public bool HasEvents { get; protected set; }

    public DbContext? MasterDbContext { get; protected set; }
    public DbConnection? Connection { get; protected set; }
    public IDbContextTransaction? Transaction { get; protected set; }
    public string? TransactionId { get; protected set; }

    public static DbOperationScope? TryGet(CommandContext context)
        => context.TryGetOperation()?.Scope as DbOperationScope;

    public abstract bool IsTransientFailure(Exception error);
    public abstract Task Commit(CancellationToken cancellationToken = default);
    public abstract ValueTask DisposeAsync();
}

public class DbOperationScope<TDbContext> : DbOperationScope
    where TDbContext : DbContext
{
    public new record Options : DbOperationScope.Options;

    private IDbShardRegistry<TDbContext>? _shardRegistry;
    private IShardDbContextFactory<TDbContext>? _contextFactory;
    private TDbContext? _masterDbContext;

    protected DbHub<TDbContext> DbHub { get; }
    protected HostId HostId { get; }
    protected IDbShardRegistry<TDbContext> ShardRegistry
        => _shardRegistry ??= Services.GetRequiredService<IDbShardRegistry<TDbContext>>();
    protected IShardDbContextFactory<TDbContext> ContextFactory
        => _contextFactory ??= Services.GetRequiredService<IShardDbContextFactory<TDbContext>>();
    protected MomentClockSet Clocks { get; }
    protected ILogger Log { get; }
    protected ILogger? DebugLog => Log.IfEnabled(LogLevel.Debug);

    protected AsyncLock AsyncLock { get; }

    public new TDbContext? MasterDbContext {
        get => _masterDbContext;
        protected set {
            _masterDbContext = value;
            base.MasterDbContext = value;
        }
    }

    public static new DbOperationScope<TDbContext>? TryGet(CommandContext context)
        => context.TryGetOperation()?.Scope as DbOperationScope<TDbContext>;

    public static DbOperationScope<TDbContext> GetOrCreate(
        CommandContext context,
        IsolationLevel isolationLevel = IsolationLevel.Unspecified)
    {
        var operation = context.TryGetOperation();
        if (operation != null)
            return operation.Scope as DbOperationScope<TDbContext>
                ?? throw Fusion.Operations.Internal.Errors.WrongOperationScopeType(
                    typeof(DbOperationScope<TDbContext>),
                    operation.Scope?.GetType());

        if (Invalidation.IsActive)
            throw Fusion.Operations.Internal.Errors.NewOperationScopeIsRequestedFromInvalidationCode();

        var outermostContext = context.OutermostContext;
        isolationLevel = GetIsolationLevelOverride(outermostContext)
            .Or(isolationLevel)
            .Or(context, static context => context.Services.GetRequiredService<Options>().IsolationLevel);
        return new DbOperationScope<TDbContext>(outermostContext, isolationLevel);
    }

    public DbOperationScope(CommandContext outermostContext, IsolationLevel isolationLevel)
    {
        CommandContext = outermostContext;
        Log = Services.LogFor(GetType());

        IsolationLevel = isolationLevel;
        DbHub = Services.DbHub<TDbContext>();
        HostId = DbHub.HostId;
        Clocks = DbHub.Clocks;
        AsyncLock = new AsyncLock();
        Operation = Operation.New(this);
        Operation.Command = outermostContext.UntypedCommand;
        outermostContext.ChangeOperation(Operation);
    }

    public override async ValueTask DisposeAsync()
    {
        using var releaser = await AsyncLock.Lock().ConfigureAwait(false);
        try {
            if (IsUsed && !IsCommitted.HasValue)
                await Rollback().ConfigureAwait(false);
        }
        catch (Exception e) {
            Log.LogWarning(e, "DisposeAsync: error on rollback");
        }
        finally {
            IsCommitted ??= false;
            try {
                if (MasterDbContext is IAsyncDisposable ad)
                    await ad.DisposeAsync().ConfigureAwait(false);
                else if (MasterDbContext is IDisposable d)
                    d.Dispose();
            }
            catch {
                // Intended
            }
        }
    }

    public virtual async ValueTask InitializeDbContext(
        TDbContext dbContext, DbShard shard, CancellationToken cancellationToken = default)
    {
        // This code must run in the same execution context to work, so
        // we run it first
        using var releaser = await AsyncLock.Lock(cancellationToken).ConfigureAwait(false);
        if (IsCommitted.HasValue)
            throw ActualLab.Fusion.Operations.Internal.Errors.OperationScopeIsAlreadyClosed();

        if (MasterDbContext == null) // !IsUsed
            await CreateMasterDbContext(shard, cancellationToken).ConfigureAwait(false);
        else if (Shard != shard)
            throw Errors.WrongDbOperationScopeShard(GetType(), Shard, shard);

        var database = dbContext.Database;
        database.DisableAutoTransactionsAndSavepoints();
        if (Connection != null) {
            var oldConnection = database.GetDbConnection();
            dbContext.SuppressDispose();
            database.SetDbConnection(Connection);
            await database.UseTransactionAsync(Transaction!.GetDbTransaction(), cancellationToken).ConfigureAwait(false);
#if !NETSTANDARD2_0
            await oldConnection.DisposeAsync().ConfigureAwait(false);
#else
            oldConnection.Dispose();
#endif
        }
    }

    public override async Task Commit(CancellationToken cancellationToken = default)
    {
        using var releaser = await AsyncLock.Lock(cancellationToken).ConfigureAwait(false);
        if (IsCommitted is { } isCommitted) {
            if (!isCommitted)
                throw ActualLab.Fusion.Operations.Internal.Errors.OperationScopeIsAlreadyClosed();
            return;
        }

        if (MasterDbContext == null) { // !IsUsed
            IsCommitted = true;
            return;
        }

        try {
            var now = Clocks.SystemClock.Now;
            Operation.LoggedAt = now;
            if (Operation.Command == null)
                throw ActualLab.Fusion.Operations.Internal.Errors.OperationHasNoCommand();

            var dbContext = MasterDbContext;
            if (dbContext.ChangeTracker.HasChanges())
                throw ActualLab.Internal.Errors.InternalError(
                    "MasterDbContext has some unsaved changes, which isn't expected here.");

            // We'll manually add/update entities here
            dbContext.EnableChangeTracking(false);
            if (Operation.Events.Count != 0) {
                var events = new Dictionary<Symbol, OperationEvent>();
                // We "clean up" the events first by getting rid of duplicates there
                foreach (var e in Operation.Events) {
                    if (ReferenceEquals(e.Value, null))
                        continue; // Events with null values cannot be processed

                    events[e.Uuid] = e;
                }
                var dbEvents = dbContext.Set<DbEvent>();
                HasEvents = events.Count != 0;
                foreach (var e in events.Values.OrderBy(x => x.Uuid)) {
                    var dbEvent = new DbEvent(e, DbHub.VersionGenerator);
                    var conflictStrategy = e.UuidConflictStrategy;
                    if (conflictStrategy == KeyConflictStrategy.Fail)
                        dbEvents.Add(dbEvent);
                    else {
                        var existingDbEvent = await dbEvents
                            .FindAsync(DbKey.Compose(dbEvent.Uuid), cancellationToken)
                            .ConfigureAwait(false);
                        if (existingDbEvent == null)
                            dbEvents.Add(dbEvent);
                        else if (conflictStrategy == KeyConflictStrategy.Update) {
                            if (existingDbEvent.State != LogEntryState.New)
                                throw KeyConflictResolver.Error<DbEvent>();

                            dbEvents.Attach(existingDbEvent);
                            existingDbEvent.UpdateFrom(e, DbHub.VersionGenerator);
                            dbEvents.Update(existingDbEvent);
                        }
                    }
                }
            }
            var dbOperation = new DbOperation(Operation);
            dbContext.Add(dbOperation);

            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            if (!dbOperation.HasIndex)
                throw Errors.DbOperationIndexWasNotAssigned();

            try {
                if (DbHub.ChaosMaker.IsEnabled)
                    await DbHub.ChaosMaker.Act(this, cancellationToken).ConfigureAwait(false);
                await Transaction!.CommitAsync(cancellationToken).ConfigureAwait(false);
                Operation.Index = dbOperation.Index;
                IsCommitted = true;
                DebugLog?.LogDebug("Transaction #{TransactionId} @ shard '{Shard}': committed", TransactionId, Shard);
            }
            catch (Exception) {
                // See https://docs.microsoft.com/en-us/ef/ef6/fundamentals/connection-resiliency/commit-failures
                try {
                    // We need a new connection here, since the old one might be broken
                    var verifierDbContext = await ContextFactory.CreateDbContextAsync(Shard, cancellationToken).ConfigureAwait(false);
                    await using var _1 = verifierDbContext.ConfigureAwait(false);
#if NET7_0_OR_GREATER
                    verifierDbContext.Database.AutoTransactionBehavior = AutoTransactionBehavior.WhenNeeded;
#else
                    verifierDbContext.Database.AutoTransactionsEnabled = true;
#endif
                    var committedDbOperation = await verifierDbContext
                        .FindAsync<DbOperation>(DbKey.Compose(dbOperation.Index), cancellationToken)
                        .ConfigureAwait(false);
                    if (committedDbOperation != null)
                        IsCommitted = true;
                }
                catch {
                    // Intended
                }
                if (IsCommitted != true)
                    throw;
            }
        }
        finally {
            IsCommitted ??= false;
        }
    }

    public override bool IsTransientFailure(Exception error)
    {
        if (error is ITransientException)
            return true;
        if (error is DbUpdateConcurrencyException)
            return true;

        try {
            var executionStrategy = MasterDbContext?.Database.CreateExecutionStrategy();
            if (executionStrategy is not ExecutionStrategy retryingExecutionStrategy)
                return false;
            var isTransient = retryingExecutionStrategy.ShouldRetryOn(error);
            return isTransient;
        }
        catch (ObjectDisposedException e) {
            // scope.MasterDbContext?.Database may throw this exception
            Log.LogWarning(e, "IsTransientFailure resorts to temporary {DbContext}", typeof(TDbContext).Name);
            try {
                var shard = ShardRegistry.HasSingleShard ? default : DbShard.Template;
                using var tmpDbContext = ContextFactory.CreateDbContext(shard);
                var executionStrategy = tmpDbContext.Database.CreateExecutionStrategy();
                return executionStrategy is ExecutionStrategy retryingExecutionStrategy
                       && retryingExecutionStrategy.ShouldRetryOn(error);
            }
            catch (Exception e2) {
                Log.LogWarning(e2, "IsTransientFailure fails for temporary {DbContext}", typeof(TDbContext).Name);
                // Intended
            }
            return false;
        }
    }

    // Protected methods

    protected virtual async ValueTask CreateMasterDbContext(DbShard shard, CancellationToken cancellationToken)
    {
        var dbContext = await ContextFactory.CreateDbContextAsync(shard, cancellationToken).ConfigureAwait(false);
        try {
            var database = dbContext.ReadWrite().Database;
            database.DisableAutoTransactionsAndSavepoints();
            var transaction = await database
                .BeginTransactionAsync(IsolationLevel, cancellationToken)
                .ConfigureAwait(false);
            if (!database.IsInMemory()) {
                Connection = database.GetDbConnection();
                if (Connection == null)
                    throw ActualLab.Internal.Errors.InternalError("No DbConnection.");
            }

            // If we're here, we can start changing properties, coz all critical actions are succeeded
            Transaction = transaction;
            TransactionId = Operation.Uuid;
            Shard = shard;
            MasterDbContext = dbContext;
            DebugLog?.LogDebug(
                "Transaction #{TransactionId} @ shard '{Shard}': started @ IsolationLevel = {IsolationLevel}",
                TransactionId, shard, IsolationLevel);
        }
        catch (Exception) {
            await dbContext.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    protected virtual async Task Rollback()
    {
        if (IsCommitted is { } isCommitted) {
            if (isCommitted)
                throw ActualLab.Fusion.Operations.Internal.Errors.OperationScopeIsAlreadyClosed();
            return;
        }

        IsCommitted = false;
        if (!IsUsed)
            return;

        DebugLog?.LogDebug("Transaction #{TransactionId} @ shard '{Shard}': rolling back", TransactionId, Shard);
        await Transaction!.RollbackAsync().ConfigureAwait(false);
    }

    // Helpers

    public static IsolationLevel GetIsolationLevelOverride(CommandContext outermostContext)
    {
        var services = outermostContext.Services;

        // 1. Try to query DbContext-specific DbIsolationLevelSelectors
        var selectors = services.GetRequiredService<IEnumerable<DbIsolationLevelSelector<TDbContext>>>();
        var isolationLevel = selectors
            .Select(x => x.Invoke(outermostContext))
            .Aggregate(IsolationLevel.Unspecified, (x, y) => x.Max(y));
        if (isolationLevel != IsolationLevel.Unspecified)
            return isolationLevel;

        // 2. Try to query DbIsolationLevelSelectors
        var globalSelectors = services.GetRequiredService<IEnumerable<DbIsolationLevelSelector>>();
        isolationLevel = globalSelectors
            .Select(x => x.Invoke(outermostContext))
            .Aggregate(IsolationLevel.Unspecified, (x, y) => x.Max(y));
        return isolationLevel;
    }


}
