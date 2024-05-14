using ActualLab.CommandR;
using ActualLab.Flows.Infrastructure;
using ActualLab.Fusion;
using ActualLab.Fusion.EntityFramework;
using ActualLab.Resilience;
using ActualLab.Versioning;
using Microsoft.EntityFrameworkCore;

namespace ActualLab.Flows.Services;

public class DbFlows : DbServiceBase, IFlows
{
    protected FlowRegistry Registry { get; }
    protected FlowSerializer Serializer { get; }
    protected IDbEntityResolver<string, DbFlow> EntityResolver { get; }
    protected IRetryPolicy GetOrStartRetryPolicy { get; init; }

    public DbFlows(IDbHub dbHub) : base(dbHub)
    {
        var services = dbHub.Services;
        Registry = services.GetRequiredService<FlowRegistry>();
        Serializer = services.GetRequiredService<FlowSerializer>();
        EntityResolver = services.DbEntityResolver<string, DbFlow>();
        GetOrStartRetryPolicy = new RetryPolicy(3, RetryDelaySeq.Exp(0.25, 1));
    }

    // [ComputeMethod]
    public virtual async Task<(byte[]? Data, long Version)> GetData(FlowId flowId, CancellationToken cancellationToken = default)
    {
        var dbFlow = await EntityResolver.Get(flowId, cancellationToken).ConfigureAwait(false);
        return (dbFlow?.Data, dbFlow?.Version ?? 0);
    }

    // [ComputeMethod]
    public virtual Task<Flow?> Get(FlowId flowId, CancellationToken cancellationToken = default)
        => Read(flowId, cancellationToken);

    // Not a [ComputeMethod]!
    public virtual Task<Flow> GetOrStart(FlowId flowId, CancellationToken cancellationToken = default)
    {
        var retryLogger = new RetryLogger(Log);
        return GetOrStartRetryPolicy.RunIsolated(async ct => {
            var flow = await Get(flowId, ct).ConfigureAwait(false);
            if (flow != null)
                return flow;

            flow = Registry.Create(flowId.Name);
            var data = Serializer.Serialize(flow);
            var setDataCommand = new FlowBackend_SetData(flow.Id, 0, data);
            var version = await Commander.Call(setDataCommand, true, ct).ConfigureAwait(false);
            flow.Initialize(flowId, version);
            return flow;
        }, retryLogger, cancellationToken);
    }

    // [CommandHandler]
    public virtual async Task<long> SetData(FlowBackend_SetData command, CancellationToken cancellationToken = default)
    {
        var (id, expectedVersion, data) = command;
        id.Require();

        if (Invalidation.IsActive) {
            _ = GetData(id, default);
            _ = Get(id, default);
            return default;
        }

        var context = CommandContext.GetCurrent();
        var shard = DbHub.ShardResolver.Resolve(id);
        var dbContext = await DbHub.CreateCommandDbContext(shard, cancellationToken).ConfigureAwait(false);
        await using var _1 = dbContext.ConfigureAwait(false);
        dbContext.EnableChangeTracking(true);

        var dbFlow = await dbContext.Set<DbFlow>().ForUpdate()
            .FirstOrDefaultAsync(x => Equals(x.Id, id.Value), cancellationToken)
            .ConfigureAwait(false);
        VersionChecker.RequireExpected(dbFlow?.Version ?? 0, expectedVersion);

        switch (dbFlow != null, data != null) {
        case (false, false): // Removed -> Removed
            break;
        case (false, true): // Create
            dbContext.Add(new DbFlow() {
                Id = id,
                Version = VersionGenerator.NextVersion(),
                Data = data,
            });
            context.Operation.AddEvent(new FlowBackend_Notify(id, null)); // Call Resume on create
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

    // Regular method
    public virtual Task<long> Notify(FlowId flowId, object? @event, CancellationToken cancellationToken = default)
    {
        flowId.Require();
        throw new NotImplementedException();
    }

    // [CommandHandler]
    public virtual Task<long> Notify(FlowBackend_Notify command, CancellationToken cancellationToken = default)
    {
        var (id, eventData) = command;
        id.Require();
        return Notify(id, Serializer.Deserialize(eventData), cancellationToken);
    }

    // Protected methods

    public virtual async Task<Flow?> Read(FlowId flowId, CancellationToken cancellationToken = default)
    {
        var dbFlow = await EntityResolver.Get(flowId, cancellationToken).ConfigureAwait(false);
        var data = dbFlow?.Data;
        if (data == null || data.Length == 0)
            return null;

        var flow = Serializer.Deserialize(data);
        flow.Initialize(flowId, dbFlow!.Version);
        return flow;
    }

}
