using ActualLab.Fusion.EntityFramework.LogProcessing;
using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework.Operations.LogProcessing;

public class DbEventLogProcessor<TDbContext>
    : DbEventLogProcessor<TDbContext, DbEvent, DbEventLogProcessor<TDbContext>.Options>
    where TDbContext : DbContext
{
    public record Options : DbEventLogProcessorOptions
    {
        public static Options Default { get; set; } = new();

#if false // Debug helper
        static Options()
        {
            var options = new Options();
            Default = options with {
                ReprocessPolicy = (RetryPolicy)options.ReprocessPolicy with { Timeout = TimeSpan.FromSeconds(0.5) }
            };
        }
#endif
    }

    protected DbOperationEventHandler OperationEventHandler { get;  }
    protected override IState<ImmutableHashSet<DbShard>> WorkerShards => DbHub.ShardRegistry.EventProcessorShards;

    // ReSharper disable once ConvertToPrimaryConstructor
    public DbEventLogProcessor(Options settings, IServiceProvider services)
        : base(settings, services)
        => OperationEventHandler = services.GetRequiredService<DbOperationEventHandler>();

    protected override Task Process(DbShard shard, DbEvent entry, CancellationToken cancellationToken)
    {
        var operationEvent = entry.ToModel();
        return OperationEventHandler.Handle(operationEvent, cancellationToken);
    }
}
