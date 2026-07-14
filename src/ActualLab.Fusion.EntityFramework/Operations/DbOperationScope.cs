using System.Data;
using System.Data.Common;
using ActualLab.CommandR.Operations;
using ActualLab.Fusion.EntityFramework.Internal;
using ActualLab.Fusion.EntityFramework.LogProcessing;
using ActualLab.Fusion.Operations.Reprocessing;
using ActualLab.Locking;
using ActualLab.Resilience;
using ActualLab.Versioning;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace ActualLab.Fusion.EntityFramework.Operations;

/// <summary>
/// Abstract base for database-backed operation scopes that manage transactions,
/// store operations and events, and handle commit verification.
/// </summary>
public abstract class DbOperationScope : IOperationScope
{
    /// <summary>
    /// Base configuration options for <see cref="DbOperationScope"/>.
    /// </summary>
    public record Options
    {
        public static IsolationLevel DefaultIsolationLevel { get; set; } = IsolationLevel.Unspecified;

        public IsolationLevel IsolationLevel { get; init; } = DefaultIsolationLevel;
        // Used only on the in-doubt commit path: a bounded, cancellation-immune read that verifies
        // whether the commit actually landed. TryTimeout bounds each attempt; the caller's token
        // is intentionally not used (see Commit).
        public IRetryPolicy CommitVerificationPolicy { get; init; } = new RetryPolicy(
            3, // Try count
            TimeSpan.FromSeconds(3), // Per-try timeout
            RetryDelaySeq.Exp(0.25, 1, 0.1, 2)); // Up to 1 second, 2x longer on each iteration
    }

    protected TaskCompletionSource<Unit>? StrategyOperationTaskSource { get; set; }
    protected Task? StrategyExecuteTask { get; set; }

    public IsolationLevel IsolationLevel { get; protected init; }
    public CommandContext CommandContext { get; protected init; } = null!;
    protected IServiceProvider Services => CommandContext.Services;
    public Operation Operation { get; protected init; } = null!;
    public string Shard { get; protected set; } = DbShard.Single;
    public bool IsTransient => false;
    public bool IsUsed => MasterDbContext is not null;
    public bool? IsCommitted { get; protected set; }
    public bool MustStoreOperation { get; set; } = true;
    public bool HasStoredOperation { get; protected set; }
    public bool HasStoredEvents { get; protected set; }
    public ImmutableList<Func<IOperationScope, Task>> CompletionHandlers { get; set; }
        = ImmutableList<Func<IOperationScope, Task>>.Empty;

    public DbContext? MasterDbContext { get; protected set; }
    public DbConnection? Connection { get; protected set; }
    public IDbContextTransaction? Transaction { get; protected set; }
    public string? TransactionId { get; protected set; }
    public IExecutionStrategy? ExecutionStrategy { get; protected set; }

    public static DbOperationScope? TryGet(CommandContext context)
        => context.TryGetOperation()?.Scope as DbOperationScope;

    public abstract bool IsTransientFailure(Exception error);
    public abstract Task Commit(CancellationToken cancellationToken = default);
    public abstract ValueTask DisposeAsync();
}

/// <summary>
/// A typed <see cref="DbOperationScope"/> bound to a specific <see cref="DbContext"/>,
/// managing the transaction lifecycle, operation/event persistence, and commit verification.
/// </summary>
public class DbOperationScope<TDbContext> : DbOperationScope
    where TDbContext : DbContext
{
    /// <summary>
    /// Configuration options for <see cref="DbOperationScope{TDbContext}"/>.
    /// </summary>
    public new record Options : DbOperationScope.Options;

    private const int MaxEventFlushRetryCount = 3;

    protected DbHub<TDbContext> DbHub { get; }
    protected HostId HostId { get; }
    protected IDbShardRegistry<TDbContext> ShardRegistry
        => field ??= Services.GetRequiredService<IDbShardRegistry<TDbContext>>();
    protected IShardDbContextFactory<TDbContext> ContextFactory
        => field ??= Services.GetRequiredService<IShardDbContextFactory<TDbContext>>();
    protected MomentClockSet Clocks { get; }
    protected Options Settings => field ??= Services.GetRequiredService<Options>();
    protected ILogger Log { get; }
    protected ILogger? DebugLog => Log.IfEnabled(LogLevel.Debug);

    protected AsyncLock AsyncLock { get; }

    public new TDbContext? MasterDbContext {
        get;
        protected set {
            field = value;
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
        if (operation is not null)
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
        // Preserve the operation Uuid across OperationReprocessor retries: a retry that re-executes
        // an operation which actually committed will then hit a unique-Uuid violation on commit,
        // which Commit turns into a "already committed" signal instead of double-executing.
        var operationUuid = outermostContext.Items.KeylessGet<IOperationReprocessor>()?.OperationUuid ?? "";
        Operation = Operation.New(this, operationUuid);
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
            Log.LogError(e, "DisposeAsync: error on rollback");
        }

        IsCommitted ??= false;
        await TryDetachExecutionStrategy().ConfigureAwait(false);
        try {
            if (MasterDbContext is IAsyncDisposable ad)
                await ad.DisposeAsync().ConfigureAwait(false);
            else if (MasterDbContext is IDisposable d)
                d.Dispose();
        }
        catch {
            // Intended
        }

        foreach (var completionHandler in CompletionHandlers) {
            try {
                await completionHandler.Invoke(this).ConfigureAwait(false);
            }
            catch (Exception e) {
                Log.LogError(e, "DisposeAsync: one of completion handlers failed");
            }
        }
    }

    public virtual async ValueTask InitializeDbContext(
        TDbContext dbContext, string shard, CancellationToken cancellationToken = default)
    {
        // This code must run in the same execution context to work, so
        // we run it first
        using var releaser = await AsyncLock.Lock(cancellationToken).ConfigureAwait(false);
        if (IsCommitted.HasValue)
            throw ActualLab.Fusion.Operations.Internal.Errors.OperationScopeIsAlreadyClosed();

        if (MasterDbContext is null) // !IsUsed
            await CreateMasterDbContext(shard, cancellationToken).ConfigureAwait(false);
        else if (!string.Equals(Shard, shard, StringComparison.Ordinal))
            throw Errors.WrongDbOperationScopeShard(GetType(), Shard, shard);

        var database = dbContext.Database;
        // Enrolled contexts share the master transaction and don't run the FlushEvents retry,
        // so they don't need auto-savepoints - only the master context does.
        database.DisableAutoTransactions(allowSavepoints: false);
        if (Connection is not null) {
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

        if (MasterDbContext is null) { // !IsUsed
            IsCommitted = true;
            return;
        }

        try {
            var now = Clocks.SystemClock.Now;
            Operation.LoggedAt = now;
            if (Operation.Command is null)
                throw ActualLab.Fusion.Operations.Internal.Errors.OperationHasNoCommand();

            var dbContext = MasterDbContext;
            if (dbContext.ChangeTracker.HasChanges())
                throw ActualLab.Internal.Errors.InternalError(
                    "MasterDbContext has some unsaved changes, which isn't expected here.");

            // We'll manually add/update entities here, so...
            dbContext.EnableChangeTracking(false);
            var versionGenerator = DbHub.VersionGenerator;

            // Creating events
            if (Operation.Events.Count != 0) {
                var events = new Dictionary<string, OperationEvent>(StringComparer.Ordinal);
                // We "clean up" the events first by getting rid of duplicates there
                foreach (var e in Operation.Events) {
                    if (ReferenceEquals(e.Value, null))
                        continue; // Events with null values cannot be processed

                    events[e.Uuid] = e;
                }
                HasStoredEvents = events.Count != 0;
                if (HasStoredEvents) {
                    var orderedEvents = events.Values.OrderBy(x => x.Uuid, StringComparer.Ordinal).ToList();
                    await FlushEvents(dbContext, orderedEvents, versionGenerator, cancellationToken).ConfigureAwait(false);
                }
            }

            // Creating either a DbOperation or DbEvent
            HasStoredOperation = MustStoreOperation;
            var dbCommitVerifier = MustStoreOperation
                ? (object)new DbOperation(Operation)
                : new DbEvent(Operation, versionGenerator);
            dbContext.Add(dbCommitVerifier);

            // Saving changes
            try {
                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (DbUpdateException updateError) {
                // A unique-Uuid violation here means a prior OperationReprocessor attempt (same
                // preserved Uuid) already committed this operation. Report success instead of
                // re-committing its DB effects - this attempt's transaction rolls back on dispose.
                bool alreadyCommitted;
                try {
                    alreadyCommitted = await VerifyCommit(dbCommitVerifier).ConfigureAwait(false);
                }
                catch {
                    throw updateError; // Can't verify -> surface the original error, let the reprocessor retry
                }
                if (!alreadyCommitted)
                    throw;

                Log.LogWarning(
                    "Transaction #{TransactionId} @ shard '{Shard}': operation already committed by a prior attempt",
                    TransactionId, Shard);
                IsCommitted = true;
                return;
            }
            if (dbCommitVerifier is DbOperation { HasIndex: false })
                throw Errors.DbOperationIndexWasNotAssigned();

            try {
                if (DbHub.ChaosMaker.IsEnabled)
                    await DbHub.ChaosMaker.Act(this, cancellationToken).ConfigureAwait(false);
                await Transaction!.CommitAsync(cancellationToken).ConfigureAwait(false);
                Operation.Index = (dbCommitVerifier as DbOperation)?.Index;
                IsCommitted = true;
                DebugLog?.LogDebug("Transaction #{TransactionId} @ shard '{Shard}': committed", TransactionId, Shard);
            }
            catch (Exception commitError) {
                // See https://docs.microsoft.com/en-us/ef/ef6/fundamentals/connection-resiliency/commit-failures
                // The commit outcome is in doubt: verify whether the row landed. This read decides
                // correctness and must not be aborted by the caller's cancellation, so VerifyCommit runs
                // on CancellationToken.None with each attempt bounded by CommitVerificationPolicy.TryTimeout.
                bool verified;
                try {
                    verified = await VerifyCommit(dbCommitVerifier).ConfigureAwait(false);
                }
                catch (Exception verifyError) {
                    Log.LogError(verifyError,
                        "Transaction #{TransactionId} @ shard '{Shard}': commit verification failed",
                        TransactionId, Shard);
                    throw commitError;
                }
                if (!verified)
                    throw;

                IsCommitted = true;
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
            var executionStrategy = ExecutionStrategy ?? MasterDbContext?.Database.CreateExecutionStrategy();
            if (executionStrategy is not ExecutionStrategy retryingExecutionStrategy)
                return false;

            var isTransient = retryingExecutionStrategy.ShouldRetryOn(error);
            return isTransient;
        }
        catch (ObjectDisposedException e) {
            // scope.MasterDbContext?.Database may throw this exception
            Log.LogWarning(e, "IsTransientFailure resorts to temporary {DbContext}", typeof(TDbContext).Name);
            try {
                var shard = ShardRegistry.HasSingleShard ? DbShard.Single : DbShard.Template;
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

    protected virtual Task<bool> VerifyCommit(object dbCommitVerifier)
    {
        var uuid = dbCommitVerifier switch {
            DbOperation dbo => dbo.Uuid,
            DbEvent dbe => dbe.Uuid,
            _ => throw ActualLab.Internal.Errors.InternalError(
                $"Unexpected commit verifier type: {dbCommitVerifier.GetType().GetName()}."),
        };
        return Settings.CommitVerificationPolicy.Apply(Verify, CancellationToken.None);

        async Task<bool> Verify(CancellationToken cancellationToken) {
            // A fresh context/connection: the scope's own connection may be broken after an in-doubt commit.
            var dbContext = await ContextFactory.CreateDbContextAsync(Shard, cancellationToken).ConfigureAwait(false);
            await using var _1 = dbContext.ConfigureAwait(false);
#if NET7_0_OR_GREATER
            dbContext.Database.AutoTransactionBehavior = AutoTransactionBehavior.WhenNeeded;
#else
            dbContext.Database.AutoTransactionsEnabled = true;
#endif
            return dbCommitVerifier is DbOperation
                ? await dbContext.Set<DbOperation>()
                    .FirstOrDefaultAsync(x => x.Uuid == uuid, cancellationToken)
                    .ConfigureAwait(false) is not null
                : await dbContext
                    .FindAsync<DbEvent>(DbKey.Compose(uuid), cancellationToken)
                    .ConfigureAwait(false) is not null;
        }
    }

    protected virtual async Task FlushEvents(
        TDbContext dbContext,
        List<OperationEvent> events,
        VersionGenerator<long> versionGenerator,
        CancellationToken cancellationToken)
    {
        // Deterministic-UUID events (e.g. delay-quantized ones with KeyConflictStrategy.Skip/Update)
        // race concurrent producers on the _events PK. EF Core creates a savepoint before each
        // SaveChanges inside our explicit transaction and rolls back to it on failure, so a unique
        // violation here doesn't doom the transaction (true even on PostgreSQL). On conflict we
        // re-read the committed rows and re-apply the per-event strategy, then retry.
        // The InMemory provider is excluded: it has no transaction/savepoint, so retrying its
        // non-atomic SaveChanges could leak partially applied rows. A batch carrying any Fail
        // event is excluded too: a Fail conflict must surface immediately rather than be retried.
        var hasFailEvents = false;
        foreach (var e in events)
            hasFailEvents |= e.UuidConflictStrategy == KeyConflictStrategy.Fail;
        var canRetry = !hasFailEvents && !dbContext.Database.IsInMemory();
        var dbEvents = dbContext.Set<DbEvent>();
        for (var attempt = 0;; attempt++) {
            if (attempt != 0) {
                // Detach the events staged by the failed attempt so FindAsync re-reads the DB
                // (a tracked Added entity would otherwise shadow the now-committed conflicting row).
                foreach (var entry in dbContext.ChangeTracker.Entries<DbEvent>().ToList())
                    entry.State = EntityState.Detached;
            }

            var mustSave = false;
            foreach (var e in events) {
                var dbEvent = new DbEvent(e, versionGenerator);
                var conflictStrategy = e.UuidConflictStrategy;
                if (conflictStrategy == KeyConflictStrategy.Fail) {
                    dbEvents.Add(dbEvent);
                    mustSave = true;
                    continue;
                }

                var existingDbEvent = await dbEvents
                    .FindAsync(DbKey.Compose(dbEvent.Uuid), cancellationToken)
                    .ConfigureAwait(false);
                if (existingDbEvent is null) {
                    dbEvents.Add(dbEvent);
                    mustSave = true;
                }
                else if (conflictStrategy == KeyConflictStrategy.Update) {
                    if (existingDbEvent.State != LogEntryState.New)
                        throw KeyConflictResolver.Error<DbEvent>();

                    dbEvents.Attach(existingDbEvent);
                    existingDbEvent.UpdateFrom(e, versionGenerator);
                    dbEvents.Update(existingDbEvent);
                    mustSave = true;
                }
                // KeyConflictStrategy.Skip with an existing row: nothing to store
            }
            if (!mustSave)
                return;

            try {
                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (Exception error) when (
                canRetry
                && error is DbUpdateException or DbUpdateConcurrencyException
                && attempt < MaxEventFlushRetryCount) {
                DebugLog?.LogDebug(error,
                    "Transaction #{TransactionId} @ shard '{Shard}': resolving _events key conflict, attempt {Attempt}",
                    TransactionId, Shard, attempt + 1);
            }
        }
    }

    protected virtual async ValueTask CreateMasterDbContext(string shard, CancellationToken cancellationToken)
    {
        var dbContext = await ContextFactory.CreateDbContextAsync(shard, cancellationToken).ConfigureAwait(false);
        try {
            var database = dbContext.ReadWrite().Database;
            // We own the transaction, so auto-transactions are off; auto-savepoints stay ON because
            // FlushEvents relies on the per-SaveChanges savepoint EF creates to recover from a
            // unique-constraint conflict on the _events flush without dooming the transaction.
            database.DisableAutoTransactions(allowSavepoints: true);
            // ExecutionStrategy is "attached" here mostly for compatibility/safety reasons -
            // it works even if the next line is commented out.
            AttachExecutionStrategy(dbContext.Database);
            var transaction = await database
                .BeginTransactionAsync(IsolationLevel, cancellationToken)
                .ConfigureAwait(false);
            if (!database.IsInMemory()) {
                Connection = database.GetDbConnection();
                if (Connection is null)
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
            await TryDetachExecutionStrategy().ConfigureAwait(false);
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

    protected virtual void AttachExecutionStrategy(DatabaseFacade database)
    {
        ExecutionStrategy = database.CreateExecutionStrategy();
        StrategyOperationTaskSource = TaskCompletionSourceExt.New<Unit>(runContinuationsAsynchronously: false);
        StrategyExecuteTask = ExecutionStrategy.ExecuteAsync(
            (Task)StrategyOperationTaskSource.Task,
            static (operationTask, _) => operationTask,
            default);
    }

    protected virtual async ValueTask TryDetachExecutionStrategy()
    {
        if (ExecutionStrategy is null)
            return;

        try {
            StrategyOperationTaskSource?.TrySetResult(default);
            if (StrategyExecuteTask is { } strategyResultTask)
                await strategyResultTask.SilentAwait(false);
        }
        finally {
            StrategyExecuteTask = null;
            StrategyOperationTaskSource = null;
            ExecutionStrategy = null;
        }
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
