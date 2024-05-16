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
    protected FlowHost FlowHost { get; }
    protected FlowSerializer Serializer { get; }
    protected IDbEntityResolver<string, DbFlow> EntityResolver { get; }

    public IRetryPolicy GetOrStartRetryPolicy { get; init; } = new RetryPolicy(3, RetryDelaySeq.Exp(0.25, 1));

    public DbFlows(DbFlowsDependencies dependencies)
        : base(dependencies.DbHub)
    {
        var services = dependencies.Services;
        Registry = services.GetRequiredService<FlowRegistry>();
        FlowHost = services.GetRequiredService<FlowHost>();
        Serializer = services.GetRequiredService<FlowSerializer>();
        EntityResolver = services.DbEntityResolver<string, DbFlow>();
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

            var flowType = Registry.Types[flowId.Name];
            Flow.RequireCorrectType(flowType);
            flow = (Flow)flowType.CreateInstance();

            var saveCommand = new Flows_Save(flow);
            var version = await Commander.Call(saveCommand, true, ct).ConfigureAwait(false);
            flow.Initialize(flowId, version);
            return flow;
        }, retryLogger, cancellationToken);
    }

    // [CommandHandler]
    public virtual async Task<long> OnSave(Flows_Save command, CancellationToken cancellationToken = default)
    {
        var (flow, expectedVersion) = command;
        var flowId = flow.Require().Id.Require();

        if (Invalidation.IsActive) {
            _ = GetData(flowId, default);
            _ = Get(flowId, default);
            return default;
        }

        var context = CommandContext.GetCurrent();
        var shard = DbHub.ShardResolver.Resolve(flowId);
        var dbContext = await DbHub.CreateCommandDbContext(shard, cancellationToken).ConfigureAwait(false);
        await using var _1 = dbContext.ConfigureAwait(false);
        dbContext.EnableChangeTracking(true);

        var dbFlow = await dbContext.Set<DbFlow>().ForUpdate()
            .FirstOrDefaultAsync(x => Equals(x.Id, flowId.Value), cancellationToken)
            .ConfigureAwait(false);
        VersionChecker.RequireExpected(dbFlow?.Version ?? 0, expectedVersion);

        switch (dbFlow != null, flow.Step != FlowSteps.MustRemove) {
        case (false, false): // Removed -> Removed
            break;
        case (false, true): // Create
            dbContext.Add(new DbFlow() {
                Id = flowId,
                Version = VersionGenerator.NextVersion(),
                Data = Serializer.Serialize(flow),
            });
            context.Operation.AddEvent(new FlowStartEvent(flowId));
            break;
        case (true, false):  // Remove
            dbContext.Remove(dbFlow!);
            break;
        case (true, true):  // Update
            dbFlow!.Version = VersionGenerator.NextVersion(dbFlow.Version);
            dbFlow.Data = Serializer.Serialize(flow);
            break;
        }

        command.EventBuilder?.Invoke(context.Operation);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return dbFlow?.Version ?? 0;
    }

    // Regular method
    public virtual Task<long> OnEvent(FlowId flowId, object? @event, CancellationToken cancellationToken = default)
        => FlowHost.HandleEvent(flowId, @event, cancellationToken);

    // [CommandHandler]
    public virtual Task<long> OnEventData(Flows_EventData command, CancellationToken cancellationToken = default)
    {
        var (_, flowId, eventData) = command;
        var @event = Serializer.DeserializeEvent(eventData);
        return FlowHost.HandleEvent(flowId, @event, cancellationToken);
    }

    // Protected methods

    public virtual async Task<Flow?> Read(FlowId flowId, CancellationToken cancellationToken = default)
    {
        var dbFlow = await EntityResolver.Get(flowId, cancellationToken).ConfigureAwait(false);
        var data = dbFlow?.Data;
        if (data == null || data.Length == 0)
            return null;

        var flow = Serializer.Deserialize(data);
        flow?.Initialize(flowId, dbFlow!.Version);
        return flow;
    }

}
