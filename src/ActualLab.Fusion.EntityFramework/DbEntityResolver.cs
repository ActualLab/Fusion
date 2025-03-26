using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq.Expressions;
using ActualLab.Fusion.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query.Internal;
using ActualLab.Fusion.EntityFramework.Internal;
using ActualLab.Net;
using ActualLab.Resilience;

namespace ActualLab.Fusion.EntityFramework;

public interface IDbEntityResolver;

public interface IDbEntityResolver<TKey, TDbEntity> : IDbEntityResolver
    where TKey : notnull
    where TDbEntity : class
{
    public Func<TDbEntity, TKey> KeyExtractor { get; init; }
    public Expression<Func<TDbEntity, TKey>> KeyExtractorExpression { get; init; }

    public Task<TDbEntity?> Get(DbShard shard, TKey key, CancellationToken cancellationToken = default);
}

/// <summary>
/// This type queues (when needed) & batches calls to <see cref="Get"/> with
/// <see cref="BatchProcessor{TIn,TOut}"/> to reduce the rate of underlying DB queries.
/// </summary>
/// <typeparam name="TDbContext">The type of <see cref="DbContext"/>.</typeparam>
/// <typeparam name="TKey">The type of entity key.</typeparam>
/// <typeparam name="TDbEntity">The type of entity to pipeline batch for.</typeparam>
[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "We assume server-side code is fully preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2060", Justification = "We assume server-side code is fully preserved")]
public class DbEntityResolver<TDbContext, TKey, TDbEntity>
    : DbServiceBase<TDbContext>, IDbEntityResolver<TKey, TDbEntity>, IAsyncDisposable
    where TDbContext : DbContext
    where TKey : notnull
    where TDbEntity : class
{
    public record Options
    {
        public static Options Default { get; set; } = new();

        public Expression<Func<TDbEntity, TKey>>? KeyExtractor { get; init; }
        public Expression<Func<IQueryable<TDbEntity>, IQueryable<TDbEntity>>>? QueryTransformer { get; init; }
        public Action<Dictionary<TKey, TDbEntity>> PostProcessor { get; init; } = _ => { };
#if NETSTANDARD2_0
        public int BatchSize { get; init; } = 5; // Max. EF.CompileQuery parameter count = 8
#else
        public int BatchSize { get; init; } = 15; // Max. EF.CompileQuery parameter count = 15
#endif
        public Action<BatchProcessor<TKey, TDbEntity?>>? ConfigureBatchProcessor { get; init; }
        public TimeSpan? Timeout { get; init; } = TimeSpan.FromSeconds(1);
        public IRetryDelayer RetryDelayer { get; init; } = new RetryDelayer() {
            Delays = RetryDelaySeq.Exp(0.125, 0.5, 0.1, 2),
            Limit = 3,
        };
        public bool IsTracingEnabled { get; init; }
    }

    // ReSharper disable once StaticMemberInGenericType
    protected static MethodInfo DbContextSetMethod { get; } = typeof(DbContext)
        .GetMethods(BindingFlags.Public | BindingFlags.Instance)
        .Single(m => Equals(m.Name, nameof(DbContext.Set)) && m.IsGenericMethod && m.GetParameters().Length == 0)
        .MakeGenericMethod(typeof(TDbEntity));
    protected static MethodInfo QueryableWhereMethod { get; }
        = new Func<IQueryable<TDbEntity>, Expression<Func<TDbEntity, bool>>, IQueryable<TDbEntity>>(Queryable.Where).Method;
    private static MethodInfo EnumerableContainsMethod { get; }
        = new Func<IEnumerable<TKey>, TKey, bool>(Enumerable.Contains).Method;

    private ConcurrentDictionary<DbShard, BatchProcessor<TKey, TDbEntity?>>? _batchProcessors;

    protected Options Settings { get; }
    protected Func<TDbContext, TKey[], IAsyncEnumerable<TDbEntity>>[] Queries { get; init; }
    protected bool UseContainsQuery { get; }

    public Func<TDbEntity, TKey> KeyExtractor { get; init; }
    public Expression<Func<TDbEntity, TKey>> KeyExtractorExpression { get; init; }
    [field: AllowNull, MaybeNull]
    public TransiencyResolver<TDbContext> TransiencyResolver =>
        field ??= Services.GetRequiredService<TransiencyResolver<TDbContext>>();

    public DbEntityResolver(Options settings, IServiceProvider services) : base(services)
    {
        Settings = settings;
        var keyExtractor = Settings.KeyExtractor;
        if (keyExtractor == null) {
            var shard = DbHub.ShardRegistry.HasSingleShard ? default : DbShard.Template;
            using var dbContext = DbHub.ContextFactory.CreateDbContext(shard);
            var keyPropertyName = dbContext.Model
                .FindEntityType(typeof(TDbEntity))!
                .FindPrimaryKey()!
                .Properties.Single().Name;

            var pEntity = Expression.Parameter(typeof(TDbEntity), "e");
            var eBody = Expression.PropertyOrField(pEntity, keyPropertyName);
            keyExtractor = Expression.Lambda<Func<TDbEntity, TKey>>(eBody, pEntity);
        }
        KeyExtractorExpression = keyExtractor;
        KeyExtractor = keyExtractor
            .Compile(preferInterpretation: RuntimeCodegen.Mode == RuntimeCodegenMode.InterpretedExpressions);
        _batchProcessors = new();

#pragma warning disable CA2214
        // ReSharper disable once VirtualMemberCallInConstructor
        UseContainsQuery = MustUseContainsQuery(typeof(TKey));
#pragma warning restore CA2214
        var queries = new List<Func<TDbContext, TKey[], IAsyncEnumerable<TDbEntity>>>(Settings.BatchSize + 1) {
            null!,
            CreateCompiledEqualsQuery(1),
        };
        if (UseContainsQuery) {
            var compiledQuery = CreateCompiledContainsQuery();
            for (var batchSize = 2; batchSize <= Settings.BatchSize; batchSize++)
                queries.Add(compiledQuery);
        }
        else {
            var batchSize = 2;
            var compiledQuery = CreateCompiledEqualsQuery(batchSize);
            for (var i = 2; i <= Settings.BatchSize; i++) {
                if (i > batchSize) {
                    batchSize = Math.Min(batchSize * 2, Settings.BatchSize);
                    compiledQuery = CreateCompiledEqualsQuery(batchSize);
                }
                queries.Add(compiledQuery);
            }
        }
        Queries = queries.ToArray();
    }

    public async ValueTask DisposeAsync()
    {
        var batchProcessors = Interlocked.Exchange(ref _batchProcessors, null);
        if (batchProcessors == null)
            return;
        await batchProcessors.Values
            .Select(p => p.DisposeAsync().AsTask())
            .Collect(CancellationToken.None)
            .ConfigureAwait(false);
    }

    public virtual Task<TDbEntity?> Get(DbShard shard, TKey key, CancellationToken cancellationToken = default)
    {
        var batchProcessor = GetBatchProcessor(shard);
        return batchProcessor.Process(key, cancellationToken);
    }

    // Protected methods

    protected virtual bool MustUseContainsQuery(Type type)
    {
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            type = type.GetGenericArguments()[0];
        return type.IsPrimitive
            || type.IsEnum
#if NET6_0_OR_GREATER
            || type == typeof(DateOnly)
            || type == typeof(TimeOnly)
#endif
            || type == typeof(DateTime)
            || type == typeof(DateTimeOffset)
            || type == typeof(TimeSpan)
            || type == typeof(Guid)
            || type == typeof(decimal)
            || type == typeof(string);
    }

    protected Func<TDbContext, TKey[], IAsyncEnumerable<TDbEntity>> CreateCompiledContainsQuery()
    {
        var pDbContext = Expression.Parameter(typeof(TDbContext), "dbContext");
        var pKeys = Expression.Parameter(typeof(TKey[]), "pKeys");
        var pEntity = Expression.Parameter(typeof(TDbEntity), "e");

        // entity.Key expression
        var eKey = KeyExtractorExpression.Body.Replace(KeyExtractorExpression.Parameters[0], pEntity);

        // .Where predicate expression
        var ePredicate = Expression.Call(EnumerableContainsMethod, pKeys, eKey);
        var lPredicate = Expression.Lambda<Func<TDbEntity, bool>>(ePredicate!, pEntity);

        // dbContext.Set<TDbEntity>().Where(...)
        var eEntitySet = Expression.Call(pDbContext, DbContextSetMethod);
        var eWhere = Expression.Call(null, QueryableWhereMethod, eEntitySet, Expression.Quote(lPredicate));

        // Applying QueryTransformer
        var qt = Settings.QueryTransformer;
        var eBody = qt == null
            ? eWhere
            : qt.Body.Replace(qt.Parameters[0], eWhere);

        // Creating compiled query
        var lambda = Expression.Lambda(eBody, [pDbContext, pKeys]);
#pragma warning disable EF1001
        var query = new CompiledAsyncEnumerableQuery<TDbContext, TDbEntity>(lambda);
#pragma warning restore EF1001

        // Locating query.Execute methods
        var mExecute = query.GetType()
            .GetMethods()
            .SingleOrDefault(m => Equals(m.Name, nameof(query.Execute))
                && m.IsGenericMethod
                && m.GetGenericArguments().Length == 1)
            ?.MakeGenericMethod(typeof(TKey[]));
        if (mExecute == null)
            throw Errors.CannotCompileQuery();

        // Creating compiled query invoker
        var eExecuteCall = Expression.Call(Expression.Constant(query), mExecute, pDbContext, pKeys);
        return (Func<TDbContext, TKey[], IAsyncEnumerable<TDbEntity>>)Expression
            .Lambda(eExecuteCall, pDbContext, pKeys)
            .Compile(preferInterpretation: RuntimeCodegen.Mode == RuntimeCodegenMode.InterpretedExpressions);
    }

    protected Func<TDbContext, TKey[], IAsyncEnumerable<TDbEntity>> CreateCompiledEqualsQuery(int batchSize)
    {
        var pDbContext = Expression.Parameter(typeof(TDbContext), "dbContext");
        var pKeys = new ParameterExpression[batchSize];
        for (var i = 0; i < batchSize; i++)
            pKeys[i] = Expression.Parameter(typeof(TKey), $"key{i.ToString(CultureInfo.InvariantCulture)}");
        var pEntity = Expression.Parameter(typeof(TDbEntity), "e");

        // entity.Key expression
        var eKey = KeyExtractorExpression.Body.Replace(KeyExtractorExpression.Parameters[0], pEntity);

        // .Where predicate expression
        var ePredicate = (Expression?)null;
        for (var i = 0; i < batchSize; i++) {
            var eCondition = Expression.Equal(eKey, pKeys[i]);
            ePredicate = ePredicate == null ? eCondition : Expression.OrElse(ePredicate, eCondition);
        }
        var lPredicate = Expression.Lambda<Func<TDbEntity, bool>>(ePredicate!, pEntity);

        // dbContext.Set<TDbEntity>().Where(...)
        var eEntitySet = Expression.Call(pDbContext, DbContextSetMethod);
        var eWhere = Expression.Call(null, QueryableWhereMethod, eEntitySet, Expression.Quote(lPredicate));

        // Applying QueryTransformer
        var qt = Settings.QueryTransformer;
        var eBody = qt == null
            ? eWhere
            : qt.Body.Replace(qt.Parameters[0], eWhere);

        // Creating compiled query
        var lambdaParameters = new ParameterExpression[batchSize + 1];
        lambdaParameters[0] = pDbContext;
        pKeys.CopyTo(lambdaParameters, 1);
        var lambda = Expression.Lambda(eBody, lambdaParameters);
#pragma warning disable EF1001
        var query = new CompiledAsyncEnumerableQuery<TDbContext, TDbEntity>(lambda);
#pragma warning restore EF1001

        // Locating query.Execute methods
        var mExecute = query.GetType()
            .GetMethods()
            .SingleOrDefault(m => Equals(m.Name, nameof(query.Execute))
                && m.IsGenericMethod
                && m.GetGenericArguments().Length == batchSize)
            ?.MakeGenericMethod(pKeys.Select(p => p.Type).ToArray());
        if (mExecute == null)
            throw Errors.BatchSizeIsTooLarge();

        // Creating compiled query invoker
        var pAllKeys = Expression.Parameter(typeof(TKey[]));
        var eDbContext = Enumerable.Range(0, 1).Select(_ => (Expression)pDbContext);
        var eAllKeys = Enumerable.Range(0, batchSize).Select(i => Expression.ArrayIndex(pAllKeys, Expression.Constant(i)));
        var eExecuteCall = Expression.Call(Expression.Constant(query), mExecute, eDbContext.Concat(eAllKeys));
        return (Func<TDbContext, TKey[], IAsyncEnumerable<TDbEntity>>)Expression
            .Lambda(eExecuteCall, pDbContext, pAllKeys)
            .Compile(preferInterpretation: RuntimeCodegen.Mode == RuntimeCodegenMode.InterpretedExpressions);
    }

    protected BatchProcessor<TKey, TDbEntity?> GetBatchProcessor(DbShard shard)
    {
        var batchProcessors = _batchProcessors;
        if (batchProcessors == null)
            throw ActualLab.Internal.Errors.AlreadyDisposed(GetType());

        return batchProcessors.GetOrAdd(shard, static (shard1, self) => self.CreateBatchProcessor(shard1), this);
    }

    protected virtual BatchProcessor<TKey, TDbEntity?> CreateBatchProcessor(DbShard shard)
    {
        var batchProcessor = new BatchProcessor<TKey, TDbEntity?> {
            BatchSize = Settings.BatchSize,
            WorkerPolicy = BatchProcessorWorkerPolicy.DbDefault,
            Implementation = (batch, cancellationToken) => ProcessBatch(shard, batch, cancellationToken),
            Log = Log,
        };
        Settings.ConfigureBatchProcessor?.Invoke(batchProcessor);
        if (batchProcessor.BatchSize != Settings.BatchSize)
            throw Errors.BatchSizeCannotBeChanged();

        return batchProcessor;
    }

    protected virtual Activity? StartProcessBatchActivity(DbShard shard, int batchSize, int tryIndex)
    {
        var activity = FusionInstruments.ActivitySource
            .IfEnabled(Settings.IsTracingEnabled)
            .StartActivity(GetType(), nameof(ProcessBatch));
        if (activity == null)
            return activity;

        activity.AddShardTags(shard)?.AddTag("batchSize", batchSize.ToString(CultureInfo.InvariantCulture));
        if (tryIndex > 0)
            activity.AddTag("tryIndex", tryIndex);
        return activity;
    }

    protected virtual async Task ProcessBatch(
        DbShard shard,
        List<BatchProcessor<TKey, TDbEntity?>.Item> batch,
        CancellationToken cancellationToken)
    {
        var batchSize = batch.Count;
        if (batchSize == 0)
            return;

        var query = Queries[batchSize];
        var tryIndex = 0;
        while (true) {
            var dbContext = await DbHub.CreateDbContext(shard, cancellationToken).ConfigureAwait(false);
            await using var _ = dbContext.ConfigureAwait(false);

            var keys = ArrayPool<TKey>.Shared.Rent(batchSize);
            var activity = StartProcessBatchActivity(shard, batchSize, tryIndex);
            try {
                var i = 0;
                foreach (var item in batch)
                    keys[i++] = item.Input;
                var lastKey = keys[i - 1];
                for (; i < batchSize; i++)
                    keys[i] = lastKey;

                var entities = new Dictionary<TKey, TDbEntity>();
                if (Settings.Timeout is { } timeout) {
                    using var cts = new CancellationTokenSource(timeout);
                    using var linkedCts = cancellationToken.LinkWith(cts.Token);
                    try {
                        var result = query.Invoke(dbContext, keys);
                        await foreach (var e in result.WithCancellation(cancellationToken).ConfigureAwait(false))
                            entities.Add(KeyExtractor.Invoke(e), e);
                    }
                    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) {
                        throw new TimeoutException();
                    }
                }
                else {
                    var result = query.Invoke(dbContext, keys);
                    await foreach (var e in result.WithCancellation(cancellationToken).ConfigureAwait(false))
                        entities.Add(KeyExtractor.Invoke(e), e);
                }
                Settings.PostProcessor.Invoke(entities);

                foreach (var item in batch) {
                    var entity = entities.GetValueOrDefault(item.Input);
                    // ReSharper disable once MethodSupportsCancellation
                    item.TrySetResult(entity);
                }
                return;
            }
            catch (Exception e) {
                activity?.Finalize(e, cancellationToken);
                if (e.IsCancellationOf(cancellationToken))
                    throw;

                var transiency = TransiencyResolver.Invoke(e);
                if (!transiency.IsTransient())
                    throw;

                if (!transiency.IsSuperTransient())
                    tryIndex++;
                var delayLogger = new RetryDelayLogger("process batch", Log);
                var delay = Settings.RetryDelayer.GetDelay(Math.Max(1, tryIndex), delayLogger, cancellationToken);
                if (delay.IsLimitExceeded)
                    throw;

                if (!delay.Task.IsCompleted)
                    await delay.Task.ConfigureAwait(false);
            }
            finally {
                activity?.Dispose();
                ArrayPool<TKey>.Shared.Return(keys);
            }
        }
    }
}
