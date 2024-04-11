using ActualLab.Fusion.EntityFramework.LogProcessing;
using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework.Operations.LogProcessing;

public class DbOperationTimerLogTrimmer<TDbContext>
    : DbTimerLogTrimmer<TDbContext, DbOperationTimer, DbOperationTimerLogTrimmer<TDbContext>.Options>
    where TDbContext : DbContext
{
    public record Options : DbLogTrimmerOptions
    {
        public static Options Default { get; set; } = new();
    }

    protected override IState<ImmutableHashSet<DbShard>> WorkerShards => DbHub.ShardRegistry.EventProcessorShards;

    public override DbLogKind LogKind => DbLogKind.Timers;

    // ReSharper disable once ConvertToPrimaryConstructor
    public DbOperationTimerLogTrimmer(Options settings, IServiceProvider services)
        : base(settings, services)
    { }
}
