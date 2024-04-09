using ActualLab.Fusion.EntityFramework.LogProcessing;
using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework.Operations;

public class DbOperationEventLogProcessor<TDbContext>
    : DbLogProcessor<TDbContext, DbOperationEvent, DbOperationEventLogProcessor<TDbContext>.Options>
    where TDbContext : DbContext
{
    public record Options : ExclusiveDbLogProcessorOptions;

    protected OperationEventProcessor OperationEventProcessor { get;  }
    protected override IState<ImmutableHashSet<DbShard>> WorkerShards => DbHub.ShardRegistry.EventProcessorShards;

    // ReSharper disable once ConvertToPrimaryConstructor
    public DbOperationEventLogProcessor(Options settings, IServiceProvider services)
        : base(settings, services)
        => OperationEventProcessor = services.GetRequiredService<OperationEventProcessor>();

    protected override Task Process(DbShard shard, DbOperationEvent entry, CancellationToken cancellationToken)
    {
        var @event = entry.ToModel();
        return OperationEventProcessor.Process(@event, cancellationToken);
    }
}
