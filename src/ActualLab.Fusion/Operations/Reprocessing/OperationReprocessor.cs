using ActualLab.Fusion.Operations.Internal;
using Errors = ActualLab.Internal.Errors;

namespace ActualLab.Fusion.Operations.Reprocessing;

/// <summary>
/// Tries to reprocess commands that failed with a reprocessable (transient) error.
/// Must be a transient service.
/// </summary>
public interface IOperationReprocessor : ICommandHandler<ICommand>
{
    void AddTransientFailure(Exception error);
    bool IsTransientFailure(IReadOnlyList<Exception> allErrors);
    bool WillRetry(IReadOnlyList<Exception> allErrors);
}

/// <summary>
/// Tries to reprocess commands that failed with a reprocessable (transient) error.
/// Must be a transient service.
/// </summary>
public class OperationReprocessor : IOperationReprocessor
{
    public record Options
    {
        public static Options Default { get; set; } = new();

        public int MaxRetryCount { get; init; } = 3;
        public RetryDelaySeq RetryDelays { get; init; } = RetryDelaySeq.Exp(0.50, 3, 0.33);
        public IMomentClock? DelayClock { get; init; }
        public Func<ICommand, CommandContext, bool> Filter { get; init; } = DefaultFilter;

        public static bool DefaultFilter(ICommand command, CommandContext context)
        {
            if (FusionSettings.Mode != FusionMode.Server)
                return false; // Only server can do the reprocessing

            // No reprocessing for commands running from scoped Commander instances,
            // i.e. no reprocessing for UI commands:
            // - the underlying backend commands are anyway reprocessed on the server side
            // - so reprocessing UI commands means N*N times reprocessing.
            return !context.Commander.Services.IsScoped();
        }
    }

    private ITransientErrorDetector<IOperationReprocessor>? _transientErrorDetector;
    private IMomentClock? _delayClock;
    private ILogger? _log;

    protected HashSet<Exception> KnownTransientFailures { get; } = new();
    protected CommandContext CommandContext { get; set; } = null!;
    protected int FailedTryCount { get; set; }
    protected Exception? LastError { get; set; }

    protected IServiceProvider Services { get; }
    protected ITransientErrorDetector<IOperationReprocessor> TransientErrorDetector
        => _transientErrorDetector ??= Services.GetRequiredService<ITransientErrorDetector<IOperationReprocessor>>();
    public IMomentClock DelayClock => _delayClock ??= Settings.DelayClock ?? Services.Clocks().CpuClock;
    protected ILogger Log => _log ??= Services.LogFor(GetType());

    public Options Settings { get; }

    // ReSharper disable once ConvertToPrimaryConstructor
    public OperationReprocessor(Options settings, IServiceProvider services)
    {
        Services = services;
        Settings = settings;
    }

    public void AddTransientFailure(Exception error)
    {
        lock (KnownTransientFailures)
            KnownTransientFailures.Add(error);
    }

    public virtual bool IsTransientFailure(IReadOnlyList<Exception> allErrors)
    {
#pragma warning disable CA1851
        lock (KnownTransientFailures) {
            // ReSharper disable once PossibleMultipleEnumeration
            if (allErrors.Any(KnownTransientFailures.Contains))
                return true;
        }
        // ReSharper disable once PossibleMultipleEnumeration
        foreach (var error in allErrors) {
            if (TransientErrorDetector.IsTransient(error)) {
                lock (KnownTransientFailures)
                    KnownTransientFailures.Add(error);
                return true;
            }
        }
#pragma warning restore CA1851
        return false;
    }

    public virtual bool WillRetry(IReadOnlyList<Exception> allErrors)
    {
        if (FailedTryCount > Settings.MaxRetryCount)
            return false;

        var operation = CommandContext.TryGetOperation();
        if (operation == null || operation.Scope is TransientOperationScope)
            return false;

        return IsTransientFailure(allErrors);
    }

    [CommandFilter(Priority = FusionOperationsCommandHandlerPriority.OperationReprocessor)]
    public virtual async Task OnCommand(ICommand command, CommandContext context, CancellationToken cancellationToken)
    {
        var isReprocessingAllowed =
            context.IsOutermost // Should be a top-level command
            && command is not ISystemCommand // No reprocessing for system commands
            && !Computed.IsInvalidating()
            && Settings.Filter.Invoke(command, context);
        if (!isReprocessingAllowed) {
            await context.InvokeRemainingHandlers(cancellationToken).ConfigureAwait(false);
            return;
        }

        if (CommandContext != null)
            throw Errors.InternalError(
                $"{GetType().GetName()} cannot be used more than once in the same command execution pipeline.");
        CommandContext = context;

        context.Items.Set((IOperationReprocessor)this);
        var itemsBackup = context.Items.Items;
        var executionStateBackup = context.ExecutionState;
        while (true) {
            try {
                await context.InvokeRemainingHandlers(cancellationToken).ConfigureAwait(false);
                LastError = null;
                break;
            }
            catch (Exception error) when (!error.IsCancellationOf(cancellationToken)) {
                LastError = error;
                FailedTryCount++;
                if (!this.WillRetry(error))
                    throw;

                context.Items.Items = itemsBackup;
                context.ExecutionState = executionStateBackup;
                var delay = Settings.RetryDelays[FailedTryCount];
                Log.LogWarning(
                    "Retry #{FailedTryCount}/{MaxTryCount} on {Error}: {Command} with {Delay} delay",
                    FailedTryCount, Settings.MaxRetryCount,
                    new ExceptionInfo(error), command, delay.ToShortString());
                await DelayClock.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
