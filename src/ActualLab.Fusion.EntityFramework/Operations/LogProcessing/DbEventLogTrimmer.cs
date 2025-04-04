using ActualLab.Fusion.EntityFramework.LogProcessing;
using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework.Operations.LogProcessing;

public class DbEventLogTrimmer<TDbContext>
    : DbEventLogTrimmer<TDbContext, DbEvent, DbEventLogTrimmer<TDbContext>.Options>
    where TDbContext : DbContext
{
    public record Options : DbLogTrimmerOptions
    {
        public static Options Default { get; set; } = new();
    }

    protected override IState<ImmutableHashSet<string>> WorkerShards => DbHub.ShardRegistry.EventProcessorShards;

    public override DbLogKind LogKind => DbLogKind.Events;

    // ReSharper disable once ConvertToPrimaryConstructor
    public DbEventLogTrimmer(Options settings, IServiceProvider services)
        : base(settings, services)
    { }
}
