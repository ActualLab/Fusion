using ActualLab.Fusion.EntityFramework.Internal;
using ActualLab.Fusion.Operations.Reprocessing;
using ActualLab.Resilience;
using RetryLimitExceededException = Microsoft.EntityFrameworkCore.Storage.RetryLimitExceededException;

namespace ActualLab.Fusion.EntityFramework.Operations;

/// <summary>
/// A command handler that manages the lifecycle of <see cref="DbOperationScope"/> for
/// outermost commands, handling commit, transient failure detection, and disposal.
/// </summary>
public class DbOperationScopeProvider(IServiceProvider services)
{
    protected IServiceProvider Services { get; } = services;
    protected ILogger Log => field ??= Services.LogFor(GetType());

    [CommandFilter(Priority = FusionEntityFrameworkCommandHandlerPriority.DbOperationScopeProvider)]
    public async Task OnCommand(ICommand command, CommandContext context, CancellationToken cancellationToken)
    {
        var isRequired =
            context.IsOutermost // Should be a top-level command
            && command is not ISystemCommand // No operations for system commands
            && !Invalidation.IsActive;
        if (!isRequired) {
            await context.InvokeRemainingHandlers(cancellationToken).ConfigureAwait(false);
            return;
        }

        try {
            await context.InvokeRemainingHandlers(cancellationToken).ConfigureAwait(false);
            if (DbOperationScope.TryGet(context) is { IsCommitted: null } scope)
                await scope.Commit(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception error) when (!error.IsCancellationOf(cancellationToken)) {
            if (error is RetryLimitExceededException { InnerException: { } innerError })
                throw innerError; // Strip RetryLimitExceededException, coz it masks the real one after 0 retries
            if (DbOperationScope.TryGet(context) is not { } scope)
                throw;

            var operationReprocessor = context.OutermostContext.Items.KeylessGet<IOperationReprocessor>();
            if (operationReprocessor is null)
                throw;

            var allErrors = error.Flatten();
            var transientError = allErrors.FirstOrDefault(scope.IsTransientFailure);
            if (transientError is null)
                throw;

            // It's a transient failure - let's tag it so that IOperationReprocessor retries on it
            operationReprocessor.MarkTransient(transientError, Transiency.Transient);

            // But if retry still doesn't happen (too many retries?) - let's log the error here
            if (!operationReprocessor.WillRetry(allErrors, out _))
                Log.LogError(error, "Operation failed: {Command}", command);
            else
                Log.LogInformation("Transient failure on {Command}: {TransientError}",
                    command, transientError.ToExceptionInfo());
            throw;
        }
        finally {
            if (DbOperationScope.TryGet(context) is { } scope)
                await scope.DisposeAsync().ConfigureAwait(false); // Triggers rollback
        }
    }
}
