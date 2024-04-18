using ActualLab.Fusion.EntityFramework.Internal;
using ActualLab.Fusion.Operations.Reprocessing;
using ActualLab.Resilience;
using RetryLimitExceededException = Microsoft.EntityFrameworkCore.Storage.RetryLimitExceededException;

namespace ActualLab.Fusion.EntityFramework.Operations;

public class DbOperationScopeProvider(IServiceProvider services)
{
    private ILogger? _log;

    protected IServiceProvider Services { get; } = services;
    protected ILogger Log => _log ??= Services.LogFor(GetType());

    [CommandFilter(Priority = FusionEntityFrameworkCommandHandlerPriority.DbOperationScopeProvider)]
    public async Task OnCommand(ICommand command, CommandContext context, CancellationToken cancellationToken)
    {
        var isRequired =
            context.IsOutermost // Should be a top-level command
            && command is not ISystemCommand // No operations for system commands
            && !Computed.IsInvalidating();
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

            var operationReprocessor = context.Items.Get<IOperationReprocessor>();
            if (operationReprocessor == null)
                throw;

            var allErrors = error.Flatten();
            var transientError = allErrors.FirstOrDefault(scope.IsTransientFailure);
            if (transientError == null)
                throw;

            // It's a transient failure - let's tag it so that IOperationReprocessor retries on it
            operationReprocessor.MarkTransient(transientError, Transiency.Transient);

            // But if retry still won't happen (too many retries?) - let's log error here
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
