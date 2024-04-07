using ActualLab.Fusion.EntityFramework.LogProcessing;
using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework.Operations;

public class DbOperationLogProcessor<TDbContext>(
    DbOperationLogProcessor<TDbContext>.Options settings,
    IServiceProvider services
    ) : DbLogProcessor<TDbContext, DbOperation>(settings, services)
    where TDbContext : DbContext
{
    public record Options : DbLogProcessorOptions;

    protected new Options Settings { get; } = settings;

    protected HostId HostId { get; } = services.GetRequiredService<HostId>();
    protected IOperationCompletionNotifier OperationCompletionNotifier { get; }
        = services.GetRequiredService<IOperationCompletionNotifier>();
    protected IDbOperationLogChangeTracker<TDbContext>? OperationLogChangeTracker { get;  }
        = services.GetService<IDbOperationLogChangeTracker<TDbContext>>();

    protected override Task Process(DbShard shard, DbOperation entry, CancellationToken cancellationToken)
    {
        var isLocal = StringComparer.Ordinal.Equals(entry.HostId, HostId.Value);
        return isLocal ? Task.CompletedTask
            : OperationCompletionNotifier.NotifyCompleted(entry.ToModel(), null);
    }

    protected override Task WhenEntriesAdded(DbShard shard, CancellationToken cancellationToken)
        => OperationLogChangeTracker?.WaitForChanges(shard, cancellationToken) ?? TaskExt.NeverEndingTask;
}
