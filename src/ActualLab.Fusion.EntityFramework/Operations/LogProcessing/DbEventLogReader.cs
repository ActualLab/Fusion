using ActualLab.Fusion.EntityFramework.LogProcessing;
using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework.Operations.LogProcessing;

public class DbEventLogReader<TDbContext>
    : DbEventLogReader<TDbContext, DbEvent, DbEventLogReader<TDbContext>.Options>
    where TDbContext : DbContext
{
    public record Options : DbEventLogReaderOptions
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

    protected DbEventProcessor<TDbContext> EventProcessor { get; }
    protected override IState<ImmutableHashSet<DbShard>> WorkerShards => DbHub.ShardRegistry.EventProcessorShards;

    // ReSharper disable once ConvertToPrimaryConstructor
    public DbEventLogReader(Options settings, IServiceProvider services)
        : base(settings, services)
        => EventProcessor = services.GetRequiredService<DbEventProcessor<TDbContext>>();

    protected override Task Process(DbShard shard, DbEvent entry, CancellationToken cancellationToken)
    {
        var operationEvent = entry.ToModel();
        return EventProcessor.Process(operationEvent, cancellationToken);
    }
}
