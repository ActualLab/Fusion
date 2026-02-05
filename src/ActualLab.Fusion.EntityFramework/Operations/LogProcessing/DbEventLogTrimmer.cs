using ActualLab.Fusion.EntityFramework.LogProcessing;
using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework.Operations.LogProcessing;

/// <summary>
/// Trims processed and discarded <see cref="DbEvent"/> entries that exceed the
/// configured maximum age.
/// </summary>
public class DbEventLogTrimmer<TDbContext>
    : DbEventLogTrimmer<TDbContext, DbEvent, DbEventLogTrimmer<TDbContext>.Options>
    where TDbContext : DbContext
{
    /// <summary>
    /// Configuration options for <see cref="DbEventLogTrimmer{TDbContext}"/>.
    /// </summary>
    public record Options : DbLogTrimmerOptions
    {
        public static Options Default { get; set; } = new();

        // Trim condition:
        // .Where(o => o.DelayUntil <= minDelayUntil && o.State != LogEntryState.New)
        public Options()
            => MaxEntryAge = TimeSpan.FromHours(1);
    }

    protected override IState<ImmutableHashSet<string>> WorkerShards => DbHub.ShardRegistry.EventProcessorShards;

    public override DbLogKind LogKind => DbLogKind.Events;

    // ReSharper disable once ConvertToPrimaryConstructor
    public DbEventLogTrimmer(Options settings, IServiceProvider services)
        : base(settings, services)
    { }
}
