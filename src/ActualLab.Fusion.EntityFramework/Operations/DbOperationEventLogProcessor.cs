using ActualLab.Fusion.EntityFramework.LogProcessing;
using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework.Operations;

public class DbOperationEventLogProcessor<TDbContext>(
    DbOperationEventLogProcessor<TDbContext>.Options settings,
    IServiceProvider services
) : DbLogProcessor<TDbContext, DbOperationEvent>(settings, services)
    where TDbContext : DbContext
{
    public record Options : ExclusiveDbLogProcessorOptions;

    protected new Options Settings { get; } = settings;

    protected OperationEventProcessor OperationEventProcessor { get;  }
        = services.GetRequiredService<OperationEventProcessor>();
    protected IOperationCompletionNotifier OperationCompletionNotifier { get; }
        = services.GetRequiredService<IOperationCompletionNotifier>();
    protected IDbOperationLogChangeTracker<TDbContext>? OperationLogChangeTracker { get;  }
        = services.GetService<IDbOperationLogChangeTracker<TDbContext>>();

    protected override Task Process(DbShard shard, DbOperationEvent entry, CancellationToken cancellationToken)
    {
        var @event = entry.ToModel();
        return OperationEventProcessor.Process(@event, cancellationToken);
    }

    protected override Task WhenEntriesAdded(DbShard shard, CancellationToken cancellationToken)
        => OperationLogChangeTracker?.WaitForChanges(shard, cancellationToken) ?? TaskExt.NeverEndingTask;
}
