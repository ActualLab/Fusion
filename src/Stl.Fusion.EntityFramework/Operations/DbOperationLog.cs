using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using ActualLab.Multitenancy;

namespace ActualLab.Fusion.EntityFramework.Operations;

public interface IDbOperationLog<
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] in TDbContext>
    where TDbContext : DbContext
{
    DbOperation New(string? id = null, string? agentId = null, object? command = null);
    Task<DbOperation> Add(TDbContext dbContext, IOperation operation, CancellationToken cancellationToken);
    Task<DbOperation?> Get(TDbContext dbContext, string id, CancellationToken cancellationToken);

    Task<List<DbOperation>> ListNewlyCommitted(Tenant tenant, DateTime minCommitTime, int maxCount, CancellationToken cancellationToken);
    Task<int> Trim(Tenant tenant, DateTime minCommitTime, int maxCount, CancellationToken cancellationToken);
}

public class DbOperationLog<
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TDbContext,
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TDbOperation>
    (IServiceProvider services)
    : DbServiceBase<TDbContext>(services),
    IDbOperationLog<TDbContext>
    where TDbContext : DbContext
    where TDbOperation : DbOperation, new()
{
    protected AgentInfo AgentInfo { get; } = services.GetRequiredService<AgentInfo>();

    public DbOperation New(string? id = null, string? agentId = null, object? command = null)
        => new TDbOperation() {
            Id = id ?? Ulid.NewUlid().ToString()!,
            AgentId = agentId ?? AgentInfo.Id,
            StartTime = Clocks.SystemClock.Now,
            Command = command,
        };

    public virtual async Task<DbOperation> Add(TDbContext dbContext,
        IOperation operation, CancellationToken cancellationToken)
    {
        // dbContext shouldn't use tracking!
        var dbOperation = (TDbOperation) operation;
        await dbContext.AddAsync((object) dbOperation, cancellationToken).ConfigureAwait(false);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return dbOperation;
    }

    public virtual async Task<DbOperation?> Get(TDbContext dbContext,
        string id, CancellationToken cancellationToken)
    {
        // dbContext shouldn't use tracking!
        var dbOperation = await dbContext.Set<TDbOperation>().AsQueryable()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            .ConfigureAwait(false);
        return dbOperation;
    }

    public virtual async Task<List<DbOperation>> ListNewlyCommitted(
        Tenant tenant, DateTime minCommitTime, int maxCount, CancellationToken cancellationToken)
    {
        var dbContext = CreateDbContext(tenant);
        await using var _ = dbContext.ConfigureAwait(false);

        var operations = await dbContext.Set<TDbOperation>().AsQueryable()
            .Where(o => o.CommitTime >= minCommitTime)
            .OrderBy(o => o.CommitTime)
            .Take(maxCount)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return operations.Cast<DbOperation>().ToList()!;
    }

    public virtual async Task<int> Trim(
        Tenant tenant, DateTime minCommitTime, int maxCount, CancellationToken cancellationToken)
    {
        var dbContext = CreateDbContext(tenant, true);
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
