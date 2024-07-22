using ActualLab.Fusion.EntityFramework.LogProcessing;
using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework.Operations.LogProcessing;

public class DbOperationLogReader<TDbContext>
    : DbOperationLogReader<TDbContext, DbOperation, DbOperationLogReader<TDbContext>.Options>
    where TDbContext : DbContext
{
    public record Options : DbOperationLogReaderOptions
    {
        public static Options Default { get; set; } = new();
    }

    protected IOperationCompletionNotifier OperationCompletionNotifier { get; }

    // ReSharper disable once ConvertToPrimaryConstructor
    public DbOperationLogReader(Options settings, IServiceProvider services)
        : base(settings, services)
        => OperationCompletionNotifier = services.GetRequiredService<IOperationCompletionNotifier>();

    protected override Task Process(DbShard shard, DbOperation entry, CancellationToken cancellationToken)
    {
        var isLocal = string.Equals(entry.HostId, DbHub.HostId.Value, StringComparison.Ordinal);
        return isLocal
            ? Task.CompletedTask
            : OperationCompletionNotifier.NotifyCompleted(entry.ToModel(), null);
    }
}
