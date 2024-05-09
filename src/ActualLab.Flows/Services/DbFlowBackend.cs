using ActualLab.Flows.Infrastructure;
using ActualLab.Fusion;
using ActualLab.Fusion.EntityFramework;
using ActualLab.Versioning;
using Microsoft.EntityFrameworkCore;

namespace ActualLab.Flows.Services;

public class DbFlowBackend : DbServiceBase, IFlowBackend
{
    protected FlowSerializer FlowSerializer { get; }
    protected IDbEntityResolver<string, DbFlow> FlowResolver { get; }

    public DbFlowBackend(IDbHub dbHub) : base(dbHub)
    {
        var services = dbHub.Services;
        FlowSerializer = services.GetRequiredService<FlowSerializer>();
        FlowResolver = services.DbEntityResolver<string, DbFlow>();
    }

    // [ComputeMethod]
    public virtual async Task<(byte[]? Data, long Version)> GetData(FlowId flowId, CancellationToken cancellationToken = default)
    {
        var dbFlow = await FlowResolver.Get(flowId, cancellationToken).ConfigureAwait(false);
        return (dbFlow?.Data, dbFlow?.Version ?? 0);
    }

    // [CommandHandler]
    public virtual async Task<long> SetData(FlowBackend_SetData command, CancellationToken cancellationToken = default)
    {
        var (id, expectedVersion, data) = command;
        id.Require();

        var shard = DbHub.ShardResolver.Resolve(id);
        if (Invalidation.IsActive) {
            _ = GetData(id, default);
            return default;
        }

        var dbContext = await DbHub.CreateCommandDbContext(shard, cancellationToken).ConfigureAwait(false);
        await using var _1 = dbContext.ConfigureAwait(false);
        dbContext.EnableChangeTracking(true);

        var dbFlow = await dbContext.Set<DbFlow>().ForUpdate()
            .FirstOrDefaultAsync(x => Equals(x.Id, id.Value), cancellationToken)
            .ConfigureAwait(false);
        VersionChecker.RequireExpected(dbFlow?.Version, expectedVersion);

        switch (dbFlow != null, data != null) {
        case (false, false): // Removed -> Removed
            break;
        case (false, true): // Create
            dbContext.Add(new DbFlow() {
                Id = id,
                Version = VersionGenerator.NextVersion(),
                Data = data,
            });
            break;
        case (true, false):  // Remove
            dbContext.Remove(dbFlow!);
            break;
        case (true, true):  // Update
            dbFlow!.Data = data;
            dbFlow.Version = VersionGenerator.NextVersion(dbFlow.Version);
            break;
        }
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return dbFlow?.Version ?? 0;
    }

    // [CommandHandler]
    public virtual async Task<long> Resume(FlowBackend_Resume command, CancellationToken cancellationToken = default)
    {
        var (id, eventData) = command;
        id.Require();

        var shard = DbHub.ShardResolver.Resolve(id);
        if (Invalidation.IsActive) {
            _ = GetData(id, default);
            return default;
        }

        throw new NotImplementedException();
    }
}
