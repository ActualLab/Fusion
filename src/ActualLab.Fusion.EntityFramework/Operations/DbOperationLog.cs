using ActualLab.CommandR.Operations;
using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework.Operations;

public interface IDbOperationLog<in TDbContext>
    where TDbContext : DbContext
{
    Task<DbOperation> Add(TDbContext dbContext, Operation operation, CancellationToken cancellationToken);
    Task<DbOperation?> Get(TDbContext dbContext, long index, CancellationToken cancellationToken);
    Task<DbOperation?> Get(TDbContext dbContext, Symbol id, CancellationToken cancellationToken);

    Task<List<DbOperation>> ListNewlyCommitted(DbShard shard, DateTime minCommitTime, int maxCount, CancellationToken cancellationToken);
    Task<int> Trim(DbShard shard, DateTime minCommitTime, int maxCount, CancellationToken cancellationToken);
}

public class DbOperationLog<TDbContext, TDbOperation>(IServiceProvider services)
    : DbServiceBase<TDbContext>(services),
    IDbOperationLog<TDbContext>
    where TDbContext : DbContext
    where TDbOperation : DbOperation, new()
{
    protected HostId HostId { get; } = services.GetRequiredService<HostId>();

    public virtual async Task<DbOperation> Add(TDbContext dbContext,
        Operation operation, CancellationToken cancellationToken)
    {
        // dbContext shouldn't use tracking!
        var dbOperation = new DbOperation();
        dbOperation.UpdateFrom(operation);
        await dbContext.AddAsync((object)dbOperation, cancellationToken).ConfigureAwait(false);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        dbOperation.Update(operation);
        return dbOperation;
    }

    public virtual async Task<DbOperation?> Get(TDbContext dbContext,
        long index, CancellationToken cancellationToken)
    {
        // dbContext shouldn't use tracking!
        var dbOperation = await dbContext.Set<TDbOperation>()
            .FindAsync(DbKey.Compose(index), cancellationToken)
            .ConfigureAwait(false);
        return dbOperation;
    }

    public virtual async Task<DbOperation?> Get(TDbContext dbContext,
        Symbol id, CancellationToken cancellationToken)
    {
        // dbContext shouldn't use tracking!
        var dbOperation = await dbContext.Set<TDbOperation>().AsQueryable()
            .FirstOrDefaultAsync(e => e.Id == id.Value, cancellationToken)
            .ConfigureAwait(false);
        return dbOperation;
    }

    public virtual async Task<List<DbOperation>> ListNewlyCommitted(
        DbShard shard, DateTime minCommitTime, int maxCount, CancellationToken cancellationToken)
    {
        var dbContext = await CreateDbContext(shard, cancellationToken).ConfigureAwait(false);
        await using var _ = dbContext.ConfigureAwait(false);

        var operations = await dbContext.Set<TDbOperation>().AsQueryable()
            .Where(o => o.CommitTime >= minCommitTime)
            .OrderBy(o => o.CommitTime)
            .Take(maxCount)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return operations.Cast<DbOperation>().ToList();
    }

    public virtual async Task<int> Trim(
        DbShard shard, DateTime minCommitTime, int maxCount, CancellationToken cancellationToken)
    {
        var dbContext = await CreateDbContext(shard, true, cancellationToken).ConfigureAwait(false);
        await using var _ = dbContext.ConfigureAwait(false);
        dbContext.EnableChangeTracking(false);

        var operations = await dbContext.Set<TDbOperation>().AsQueryable()
            .Where(o => o.CommitTime < minCommitTime)
            .OrderBy(o => o.CommitTime)
            .Take(maxCount)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        if (operations.Count == 0)
            return 0;
        foreach (var operation in operations)
            dbContext.Remove(operation);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return operations.Count;
    }
}
