using System.Data;
using System.Data.Common;
using ActualLab.CommandR.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ActualLab.Fusion.EntityFramework.Internal;
using ActualLab.Fusion.EntityFramework.Operations;
using ActualLab.Locking;

namespace ActualLab.Fusion.EntityFramework;

public interface IDbOperationScope : IOperationScope
{
    DbContext? MasterDbContext { get; }
    DbConnection? Connection { get; }
    IDbContextTransaction? Transaction { get; }
    string? TransactionId { get; }
    IsolationLevel IsolationLevel { get; set; }
    DbShard Shard { get; }

    ValueTask InitializeDbContext(DbContext preCreatedDbContext, DbShard shard, CancellationToken cancellationToken = default);
    bool IsTransientFailure(Exception error);
}

public class DbOperationScope<TDbContext> : SafeAsyncDisposableBase, IDbOperationScope
    where TDbContext : DbContext
{
    public record Options
    {
        public IsolationLevel DefaultIsolationLevel { get; init; } = IsolationLevel.Unspecified;
    }

    private IDbShardRegistry<TDbContext>? _shardRegistry;
    private IShardDbContextFactory<TDbContext>? _contextFactory;
    private DbShard _shard = DbShard.None;

    public Options Settings { get; protected init; }
    DbContext? IDbOperationScope.MasterDbContext => MasterDbContext;
    public TDbContext? MasterDbContext { get; protected set; }
    public DbConnection? Connection { get; protected set; }
    public IDbContextTransaction? Transaction { get; protected set; }
    public string? TransactionId { get; protected set; }
    public IsolationLevel IsolationLevel { get; set; } = IsolationLevel.Unspecified;

    public Operation Operation { get; protected init; }
    public CommandContext CommandContext { get; protected init; }
    public bool AllowsEvents => true;
    public bool IsUsed => MasterDbContext != null;
    public bool IsClosed { get; private set; }
    public bool? IsConfirmed { get; private set; }

    public DbShard Shard {
        get => _shard;
        protected set {
            if (_shard == value)
                return;

            _shard = !IsUsed ? value : throw Errors.DbOperationScopeIsAlreadyUsed();
        }
    }

    // Services
    protected IServiceProvider Services { get; }
    protected HostId HostId { get; }
    protected IDbShardRegistry<TDbContext> ShardRegistry
        => _shardRegistry ??= Services.GetRequiredService<IDbShardRegistry<TDbContext>>();
    protected IShardDbContextFactory<TDbContext> ContextFactory
        => _contextFactory ??= Services.GetRequiredService<IShardDbContextFactory<TDbContext>>();
    protected MomentClockSet Clocks { get; }
    protected AsyncLock AsyncLock { get; }
    protected ILogger Log { get; }

    public DbOperationScope(Options settings, IServiceProvider services)
    {
        Settings = settings;
        Services = services;
        Log = Services.LogFor(GetType());
        CommandContext = CommandContext.GetCurrent();
        var commanderHub = CommandContext.Commander.Hub;
        HostId = commanderHub.HostId;
        Clocks = commanderHub.Clocks;
        AsyncLock = new AsyncLock(LockReentryMode.CheckedPass);
        Operation = Operation.New(this);
    }

    protected override async Task DisposeAsync(bool disposing)
    {
        // Intentionally ignore disposing flag here

        using var releaser = await AsyncLock.Lock().ConfigureAwait(false);
        releaser.MarkLockedLocally();

        try {
            if (IsUsed && !IsClosed)
                await Rollback().ConfigureAwait(false);
        }
        catch (Exception e) {
            Log.LogWarning(e, "DisposeAsync: error on rollback");
        }
        finally {
            IsClosed = true;
            SilentDispose(MasterDbContext);
        }

        void SilentDispose(IDisposable? d) {
            try {
                d?.Dispose();
            }
            catch {
                // Intended
            }
        }
    }

    async ValueTask IDbOperationScope.InitializeDbContext(
        DbContext dbContext, DbShard shard, CancellationToken cancellationToken)
        => await InitializeDbContext((TDbContext)dbContext, shard, cancellationToken).ConfigureAwait(false);
    public virtual async ValueTask InitializeDbContext(
        TDbContext dbContext, DbShard shard, CancellationToken cancellationToken = default)
    {
        // This code must run in the same execution context to work, so
        // we run it first
        using var releaser = await AsyncLock.Lock(cancellationToken).ConfigureAwait(false);
        releaser.MarkLockedLocally();

        Shard = shard;
        if (IsClosed)
            throw ActualLab.Fusion.Operations.Internal.Errors.OperationScopeIsAlreadyClosed();

        if (MasterDbContext == null)
            await CreateMasterDbContext(cancellationToken).ConfigureAwait(false);

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
        CommandContext.ChangeOperation(Operation, true);
    }

    public virtual async Task Commit(CancellationToken cancellationToken = default)
    {
        using var releaser = await AsyncLock.Lock(cancellationToken).ConfigureAwait(false);
        if (IsClosed) {
            if (IsConfirmed == true)
                return;
            throw ActualLab.Fusion.Operations.Internal.Errors.OperationScopeIsAlreadyClosed();
        }

        releaser.MarkLockedLocally();
        try {
            if (!IsUsed) {
                IsConfirmed = true;
                return;
            }

            Operation.LoggedAt = Clocks.SystemClock.Now;
            if (Operation.Command == null)
                throw ActualLab.Fusion.Operations.Internal.Errors.OperationHasNoCommand();

            var dbContext = MasterDbContext!;
            dbContext.EnableChangeTracking(false); // Just to speed up things a bit
            if (Operation.HasEvents) {
                foreach (var @event in Operation.Events) {
                    var dbOperationEvent = new DbOperationEvent(@event);
                    dbContext.Add(dbOperationEvent);
                }
            }
            var dbOperation = new DbOperation(Operation);
            dbContext.Add(dbOperation);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            if (!dbOperation.HasIndex)
                throw Errors.DbOperationIndexWasNotAssigned();

            try {
                if (DbOperationsChaosMonkey.Instance is { } chaosMonkey) {
                    if (chaosMonkey.CommitDelaySampler.Next.Invoke())
                        await Task.Delay(chaosMonkey.CommitDelay.Next(), cancellationToken).ConfigureAwait(false);
                    if (chaosMonkey.CommitFailureSampler.Next.Invoke())
                        throw new TransientException("DbOperationsChaosMonkey-caused failure.");
                }
                await Transaction!.CommitAsync(cancellationToken).ConfigureAwait(false);
                Operation.Index = dbOperation.Index;
                IsConfirmed = true;
                Log.IfEnabled(LogLevel.Debug)?.LogDebug("Transaction #{TransactionId}: committed", TransactionId);
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
                        IsConfirmed = true;
                }
                catch {
                    // Intended
                }
                if (IsConfirmed != true)
                    throw;
            }
        }
        finally {
            IsConfirmed ??= false;
            IsClosed = true;
        }
    }

    public virtual async Task Rollback()
    {
        using var releaser = await AsyncLock.Lock().ConfigureAwait(false);
        if (IsClosed) {
            if (IsConfirmed == false)
                return;

            throw ActualLab.Fusion.Operations.Internal.Errors.OperationScopeIsAlreadyClosed();
        }

        releaser.MarkLockedLocally();
        try {
            if (!IsUsed)
                return;

            await Transaction!.RollbackAsync().ConfigureAwait(false);
        }
        finally {
            Log.IfEnabled(LogLevel.Debug)?.LogDebug("Transaction #{TransactionId}: rolled back", TransactionId);
            IsConfirmed ??= false;
            IsClosed = true;
        }
    }

    public virtual bool IsTransientFailure(Exception error)
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

    protected virtual async ValueTask CreateMasterDbContext(CancellationToken cancellationToken)
    {
        var dbContext = await ContextFactory.CreateDbContextAsync(Shard, cancellationToken).ConfigureAwait(false);
        var database = dbContext.ReadWrite().Database;
        database.DisableAutoTransactionsAndSavepoints();

        var commandContext = CommandContext;
        // 1. If IsolationLevel is set explicitly, we honor it
        var isolationLevel = IsolationLevel;
        if (isolationLevel != IsolationLevel.Unspecified)
            goto ready;

        // 2. Try to query DbContext-specific DbIsolationLevelSelectors
        var selectors = Services.GetRequiredService<IEnumerable<DbIsolationLevelSelector<TDbContext>>>();
        isolationLevel = selectors
            .Select(x => x.Invoke(commandContext))
            .Aggregate(IsolationLevel.Unspecified, (x, y) => x.Max(y));
        if (isolationLevel != IsolationLevel.Unspecified)
            goto ready;

        // 3. Try to query GlobalIsolationLevelSelectors
        var globalSelectors = Services.GetRequiredService<IEnumerable<DbIsolationLevelSelector>>();
        isolationLevel = globalSelectors
            .Select(x => x.Invoke(commandContext))
            .Aggregate(IsolationLevel.Unspecified, (x, y) => x.Max(y));
        if (isolationLevel != IsolationLevel.Unspecified)
            goto ready;

        // 4. Use Settings.DefaultIsolationLevel
        isolationLevel = Settings.DefaultIsolationLevel;

        ready:
        IsolationLevel = isolationLevel;
        Transaction = await database
            .BeginTransactionAsync(isolationLevel, cancellationToken)
            .ConfigureAwait(false);
        TransactionId = Operation.Uuid;
        if (!database.IsInMemory()) {
            Connection = database.GetDbConnection();
            if (Connection == null)
                throw ActualLab.Internal.Errors.InternalError("No DbConnection.");
        }
        MasterDbContext = dbContext;
        CommandContext.Items.Replace<IOperationScope?>(null, this);

        Log.IfEnabled(LogLevel.Debug)?.LogDebug(
            "Transaction #{TransactionId}: started @ IsolationLevel = {IsolationLevel}",
            TransactionId, isolationLevel);
    }
}
