using ActualLab.CommandR.Operations;
using ActualLab.Fusion.EntityFramework.LogProcessing;
using ActualLab.Resilience;
using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework.Operations;

public class DbOperationCompletionListener<TDbContext>
    : DbProcessorBase<TDbContext>, IOperationCompletionListener
    where TDbContext : DbContext
{
    public record Options
    {
        public static Options Default { get; set; } = new();

        public IRetryPolicy NotifyRetryPolicy { get; init; } = new RetryPolicy(
            TimeSpan.FromSeconds(10),
            RetryDelaySeq.Exp(0.25, 1, 0.1, 2)) {
            RetryOnNonTransient = true,
        };
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

        if (operation.Scope is not DbOperationScope<TDbContext> operationScope)
            return Task.CompletedTask; // Not our own scope

        // TransientOperationScope already does the same check, but just in case:
        if (operationScope is not { IsUsed: true, IsCommitted: true })
            return Task.CompletedTask; // Nothing is committed

        var shard = operationScope.Shard;
        _ = Settings.NotifyRetryPolicy.RunIsolated(
            ct => Notify(shard, operation, operationScope, ct),
            new RetryLogger(Log, $"{nameof(Notify)}[{shard}]"));
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

        var notifyEventLogTask = EventLogWatcher.NotifyChanged(shard, cancellationToken);
        return Task.WhenAll(notifyOperationLogTask, notifyEventLogTask);
    }
}
