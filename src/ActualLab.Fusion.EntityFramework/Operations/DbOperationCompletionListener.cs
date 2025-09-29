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
            2, // Retry just once: both operation and event log readers have unconditional check periods
            TimeSpan.FromSeconds(10),
            RetryDelaySeq.Exp(0.25, 1, 0.1, 2));
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
        if (commandContext is null)
            return Task.CompletedTask; // Not a local operation

        if (operation.Scope is not DbOperationScope<TDbContext> operationScope)
            return Task.CompletedTask; // Not our own scope

        // TransientOperationScope already does the same check, but just in case:
        if (operationScope is not { IsUsed: true, IsCommitted: true })
            return Task.CompletedTask; // Nothing is committed

        var shard = operationScope.Shard;
        _ = Settings.NotifyRetryPolicy.RunIsolated(
            ct => OnLocalOperationCompleted(shard, operation, operationScope, ct),
            new RetryLogger(Log, $"{nameof(OnLocalOperationCompleted)}[{shard}]"));
        return Task.CompletedTask;
    }

    // Protected methods

    protected virtual Task OnLocalOperationCompleted(
        string shard, Operation operation, DbOperationScope<TDbContext> operationScope,
        CancellationToken cancellationToken)
    {
        var notifyTask1 = operationScope.CreatedOperation
            ? OperationLogWatcher.NotifyChanged(shard, cancellationToken)
            : null;
        var notifyTask2 = operationScope.CreatedEvents
            ? EventLogWatcher.NotifyChanged(shard, cancellationToken)
            : null;

        if (notifyTask1 is null)
            return notifyTask2 ?? Task.CompletedTask;
        if (notifyTask2 is null)
            return notifyTask1;
        return Task.WhenAll(notifyTask1, notifyTask2);
    }
}
