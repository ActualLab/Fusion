using ActualLab.Fusion.EntityFramework.LogProcessing;
using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework.Operations.LogProcessing;

public class DbOperationLogProcessor<TDbContext>
    : DbLogProcessor<TDbContext, DbOperation, DbOperationLogProcessor<TDbContext>.Options>
    where TDbContext : DbContext
{
    public record Options : DbOperationLogProcessorOptions
    {
        public static Options Default { get; set; } = new();
    }

    protected IOperationCompletionNotifier OperationCompletionNotifier { get; }

    // ReSharper disable once ConvertToPrimaryConstructor
    public DbOperationLogProcessor(Options settings, IServiceProvider services)
        : base(settings, services)
        => OperationCompletionNotifier = services.GetRequiredService<IOperationCompletionNotifier>();

    protected override Task Process(DbShard shard, DbOperation entry, CancellationToken cancellationToken)
    {
        var isLocal = StringComparer.Ordinal.Equals(entry.HostId, DbHub.HostId.Value);
        return isLocal
            ? Task.CompletedTask
            : OperationCompletionNotifier.NotifyCompleted(entry.ToModel(), null);
    }
}
