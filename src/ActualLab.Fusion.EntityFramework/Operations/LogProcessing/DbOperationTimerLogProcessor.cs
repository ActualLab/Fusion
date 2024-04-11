using ActualLab.Fusion.EntityFramework.LogProcessing;
using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework.Operations.LogProcessing;

public class DbOperationTimerLogProcessor<TDbContext>
    : DbTimerLogProcessor<TDbContext, DbOperationTimer, DbOperationTimerLogProcessor<TDbContext>.Options>
    where TDbContext : DbContext
{
    public record Options : DbTimerLogProcessorOptions
    {
        public static Options Default { get; set; } = new();
    }

    protected DbOperationEventHandler OperationEventHandler { get;  }
    protected override IState<ImmutableHashSet<DbShard>> WorkerShards => DbHub.ShardRegistry.EventProcessorShards;

    // ReSharper disable once ConvertToPrimaryConstructor
    public DbOperationTimerLogProcessor(Options settings, IServiceProvider services)
        : base(settings, services)
        => OperationEventHandler = services.GetRequiredService<DbOperationEventHandler>();

    protected override Task Process(DbShard shard, DbOperationTimer entry, CancellationToken cancellationToken)
    {
        var operationEvent = entry.ToModel();
        return OperationEventHandler.Handle(operationEvent, cancellationToken);
    }
}
