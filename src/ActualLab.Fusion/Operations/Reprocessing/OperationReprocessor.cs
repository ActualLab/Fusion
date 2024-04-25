using ActualLab.Fusion.Operations.Internal;
using ActualLab.Resilience;
using Errors = ActualLab.Internal.Errors;

namespace ActualLab.Fusion.Operations.Reprocessing;

/// <summary>
/// Tries to reprocess commands that failed with a reprocessable (transient) error.
/// Must be a transient service.
/// </summary>
public interface IOperationReprocessor : ICommandHandler<ICommand>
{
    void MarkTransient(Exception error, Transiency transiency);
    Transiency GetTransiency(IReadOnlyList<Exception> allErrors);
    bool WillRetry(IReadOnlyList<Exception> allErrors, out Transiency transiency);
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
            if (FusionDefaults.Mode != FusionMode.Server)
                return false; // Only server can do the reprocessing

            // No reprocessing for commands running from scoped Commander instances,
            // i.e. no reprocessing for UI commands:
            // - the underlying backend commands are anyway reprocessed on the server side
            // - so reprocessing UI commands means N*N times reprocessing.
            return !context.Commander.Services.IsScoped();
        }
    }

    private TransiencyResolver<IOperationReprocessor>? _transiencyResolver;
    private IMomentClock? _delayClock;
    private ILogger? _log;

    protected Dictionary<Exception, Transiency> KnownTransiencies { get; } = new();
    protected CommandContext CommandContext { get; set; } = null!;
    protected int TryIndex { get; set; }
    protected Exception? LastError { get; set; }

    protected IServiceProvider Services { get; }
    protected TransiencyResolver<IOperationReprocessor> TransiencyResolver
        => _transiencyResolver ??= Services.TransiencyResolver<IOperationReprocessor>();
    public IMomentClock DelayClock => _delayClock ??= Settings.DelayClock ?? Services.Clocks().CpuClock;
    protected ILogger Log => _log ??= Services.LogFor(GetType());

    public Options Settings { get; }

    // ReSharper disable once ConvertToPrimaryConstructor
    public OperationReprocessor(Options settings, IServiceProvider services)
    {
        Services = services;
        Settings = settings;
    }

    public void MarkTransient(Exception error, Transiency transiency)
    {
        if (!transiency.IsTransient())
            throw new ArgumentOutOfRangeException(nameof(transiency));

        lock (KnownTransiencies)
            KnownTransiencies.Add(error, transiency);
    }

    public virtual Transiency GetTransiency(IReadOnlyList<Exception> allErrors)
    {
        lock (KnownTransiencies) {
            // ReSharper disable once PossibleMultipleEnumeration
            foreach (var error in allErrors)
                if (KnownTransiencies.TryGetValue(error, out var transiency))
                    return transiency;
        }
        // ReSharper disable once PossibleMultipleEnumeration
        foreach (var error in allErrors) {
            var transiency = TransiencyResolver.Invoke(error);
            if (transiency.IsNonTransient())
                continue;

            lock (KnownTransiencies)
                KnownTransiencies.Add(error, transiency);
            return transiency;
        }
        return Transiency.Unknown;
    }

    public virtual bool WillRetry(IReadOnlyList<Exception> allErrors, out Transiency transiency)
    {
        transiency = GetTransiency(allErrors);
        if (transiency.IsNonTransient())
            return false;

        if (!transiency.IsSuperTransient() && TryIndex >= Settings.MaxRetryCount)
            return false;

        var operation = CommandContext.TryGetOperation();
        return operation is { Scope: not TransientOperationScope };
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
        var itemsBackup = context.Items.Snapshot;
        var executionStateBackup = context.ExecutionState;
        while (true) {
            try {
                await context.InvokeRemainingHandlers(cancellationToken).ConfigureAwait(false);
                LastError = null;
                break;
            }
            catch (Exception error) when (!error.IsCancellationOf(cancellationToken)) {
                LastError = error;
                if (!this.WillRetry(error, out var transiency))
                    throw;

                if (!transiency.IsSuperTransient())
                    TryIndex++;
                context.Items.Snapshot = itemsBackup;
                context.ExecutionState = executionStateBackup;
                var delay = Settings.RetryDelays[TryIndex];
                Log.LogWarning(
                    "Retry #{TryIndex}/{MaxTryCount} on {Error}: {Command} with {Delay} delay",
                    TryIndex, Settings.MaxRetryCount,
                    new ExceptionInfo(error), command, delay.ToShortString());
                await DelayClock.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
