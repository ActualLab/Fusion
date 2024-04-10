using ActualLab.CommandR.Operations;
using ActualLab.Fusion.EntityFramework.LogProcessing;
using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework.Operations;

public class DbOperationCompletionListener<TDbContext>
    : DbServiceBase<TDbContext>, IOperationCompletionListener
    where TDbContext : DbContext
{
    public record Options
    {
        public static Options Default { get; set; } = new();

        public int? NotifyRetryCount { get; init; } = 3;
        public RetryDelaySeq NotifyRetryDelays { get; init; } = RetryDelaySeq.Linear(0.1);
    }

    protected Options Settings { get; init; }

    protected IDbLogWatcher<TDbContext, DbOperation> LogWatcher { get; }
    protected IDbLogWatcher<TDbContext, DbOperationEvent> EventLogWatcher { get; }

    // ReSharper disable once ConvertToPrimaryConstructor
    public DbOperationCompletionListener(Options settings, IServiceProvider services) : base(services)
    {
        Settings = settings;
        LogWatcher = services.DbLogWatcher<TDbContext, DbOperation>();
        EventLogWatcher = services.DbLogWatcher<TDbContext, DbOperationEvent>();
    }

    public Task OnOperationCompleted(Operation operation, CommandContext? commandContext)
    {
        if (commandContext == null)
            return Task.CompletedTask; // Not a local operation

        var operationScope = commandContext.Items.Get<DbOperationScope<TDbContext>>();
        if (operationScope is not { IsConfirmed: true })
            return Task.CompletedTask; // Nothing is committed to TDbContext

        var shard = operationScope.Shard;
        var notifyChain = new AsyncChain($"Notify({shard})", ct => Notify(shard, operation, ct))
            .Retry(Settings.NotifyRetryDelays, Settings.NotifyRetryCount, Clocks.CpuClock, Log);
        _ = notifyChain.RunIsolated(CancellationToken.None);
        return Task.CompletedTask;
    }

    // Protected methods

    protected virtual Task Notify(DbShard shard, Operation operation, CancellationToken cancellationToken)
    {
        var notifyOperationLogTask = LogWatcher.NotifyChanged(shard, cancellationToken);
        if (operation.Events.Count == 0)
            return notifyOperationLogTask;

        var notifyOperationEventLogTask = EventLogWatcher.NotifyChanged(shard, cancellationToken);
        return Task.WhenAll(notifyOperationLogTask, notifyOperationEventLogTask);
    }
}
