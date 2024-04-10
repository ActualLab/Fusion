using ActualLab.Fusion.EntityFramework.LogProcessing;
using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework.Operations.LogProcessing;

public class DbOperationEventLogProcessor<TDbContext>
    : DbLogProcessor<TDbContext, DbOperationEvent, DbOperationEventLogProcessor<TDbContext>.Options>
    where TDbContext : DbContext
{
    public record Options : ExclusiveDbLogProcessorOptions
    {
        public static Options Default { get; set; } = new();

#if false // Debug helper
        static Options()
        {
            var options = new Options();
            Default = options with {
                GapRetryPolicy = (RetryPolicy)options.GapRetryPolicy with { Timeout = TimeSpan.FromSeconds(0.5) }
            };
        }
#endif
    }

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
