using ActualLab.CommandR.Operations;
using ActualLab.Fusion.EntityFramework.LogProcessing;
using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework.Operations;

public class DbOperationCompletionListener<TDbContext>
    : DbProcessorBase<TDbContext>, IOperationCompletionListener
    where TDbContext : DbContext
{
    public record Options
    {
        public static Options Default { get; set; } = new();

        public int? NotifyRetryCount { get; init; } = 3;
        public RetryDelaySeq NotifyRetryDelays { get; init; } = RetryDelaySeq.Linear(0.1);
    }

    protected Options Settings { get; init; }

    protected IDbLogWatcher<TDbContext, DbOperation> OperationLogWatcher { get; }
    protected IDbLogWatcher<TDbContext, DbEvent> EventLogWatcher { get; }

    // ReSharper disable once ConvertToPrimaryConstructor
    public DbOperationCompletionListener(Options settings, IServiceProvider services) : base(services)
    {
        Settings = settings;
        OperationLogWatcher = services.GetRequiredService<IDbLogWatcher<TDbContext, DbOperation>>();
        EventLogWatcher = services.GetRequiredService<IDbLogWatcher<TDbContext, DbEvent>>();
    }

    public Task OnOperationCompleted(Operation operation, CommandContext? commandContext)
    {
        if (commandContext == null)
            return Task.CompletedTask; // Not a local operation

        var operationScope = commandContext.Items.Get<DbOperationScope<TDbContext>>();
        if (operationScope is not { IsConfirmed: true })
            return Task.CompletedTask; // Nothing is committed to TDbContext

        var shard = operationScope.Shard;
        _ = new AsyncChain($"Notify({shard})", ct => Notify(shard, operation, operationScope, ct))
            .Retry(Settings.NotifyRetryDelays, Settings.NotifyRetryCount, Clocks.CpuClock, Log)
            .RunIsolated(StopToken);
        return Task.CompletedTask;
    }

    // Protected methods

    protected virtual Task Notify(
        DbShard shard, Operation operation, DbOperationScope<TDbContext> operationScope,
        CancellationToken cancellationToken)
    {
        var notifyOperationLogTask = OperationLogWatcher.NotifyChanged(shard, cancellationToken);
        if (!operationScope.HasEvents)
            return notifyOperationLogTask;

        var notifyOperationEventLogTask = EventLogWatcher.NotifyChanged(shard, cancellationToken);
        return Task.WhenAll(notifyOperationLogTask, notifyOperationEventLogTask);
    }
}
