using System.Diagnostics.CodeAnalysis;
using ActualLab.Fusion.Operations.Internal;
using ActualLab.OS;
using ActualLab.Resilience;
using Errors = ActualLab.Internal.Errors;

namespace ActualLab.Fusion.Operations.Reprocessing;

/// <summary>
/// Tries to reprocess commands that failed with a reprocessable (transient) error.
/// Must be a transient service.
/// </summary>
public interface IOperationReprocessor : ICommandHandler<ICommand>
{
    public void MarkTransient(Exception error, Transiency transiency);
    public Transiency GetTransiency(IReadOnlyList<Exception> allErrors);
    public bool WillRetry(IReadOnlyList<Exception> allErrors, out Transiency transiency);
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
        public MomentClock? DelayClock { get; init; }
        public Func<ICommand, CommandContext, bool> Filter { get; init; } = DefaultFilter;

        public static bool DefaultFilter(ICommand command, CommandContext context)
        {
            if (!RuntimeInfo.IsServer)
                return false; // Only server can do the reprocessing
            if (command is IDelegatingCommand)
                return false; // No reprocessing for IDelegatingCommand

            // No reprocessing for commands running from scoped Commander instances,
            // i.e., no reprocessing for UI commands:
            // - the underlying backend commands are anyway reprocessed on the server side
            // - so reprocessing UI commands means N*N times reprocessing.
            return !context.Commander.Services.IsScoped();
        }
    }

    protected Dictionary<Exception, Transiency> KnownTransiencies { get; } = new();
    protected CommandContext CommandContext { get; set; } = null!;
    protected int TryIndex { get; set; }
    protected Exception? LastError { get; set; }

    protected IServiceProvider Services { get; }

    [field: AllowNull, MaybeNull]
    protected TransiencyResolver<IOperationReprocessor> TransiencyResolver
        => field ??= Services.TransiencyResolver<IOperationReprocessor>();

    [field: AllowNull, MaybeNull]
    public MomentClock DelayClock => field ??= Settings.DelayClock ?? Services.Clocks().CpuClock;

    [field: AllowNull, MaybeNull]
    protected ILogger Log => field ??= Services.LogFor(GetType());

    public Options Settings { get; }

    // ReSharper disable once ConvertToPrimaryConstructor
    public OperationReprocessor(Options settings, IServiceProvider services)
    {
        Services = services;
        Settings = settings;
    }

    public void MarkTransient(Exception error, Transiency transiency)
    {
        if (!transiency.IsAnyTransient())
            throw new ArgumentOutOfRangeException(nameof(transiency));

        lock (KnownTransiencies)
            KnownTransiencies[error] = transiency;
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
            if (!transiency.IsAnyTransient())
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
        if (!transiency.IsAnyTransient())
            return false;

        if (transiency is not Transiency.SuperTransient && TryIndex >= Settings.MaxRetryCount)
            return false;

        var operation = CommandContext.TryGetOperation();
        return operation is { Scope: not InMemoryOperationScope };
    }

    [CommandFilter(Priority = FusionOperationsCommandHandlerPriority.OperationReprocessor)]
    public virtual async Task OnCommand(ICommand command, CommandContext context, CancellationToken cancellationToken)
    {
        var isReprocessingAllowed =
            context.IsOutermost // Should be a top-level command
            && command is not ISystemCommand // No reprocessing for system commands
            && context.TryGetOperation() is null // Operation isn't started yet
            && !Invalidation.IsActive // No invalidation is running
            && Settings.Filter.Invoke(command, context);
        if (!isReprocessingAllowed) {
            await context.InvokeRemainingHandlers(cancellationToken).ConfigureAwait(false);
            return;
        }

        if (CommandContext is not null)
            throw Errors.InternalError(
                $"{GetType().GetName()} cannot be used more than once in the same command execution pipeline.");
        CommandContext = context;

        context.Items.KeylessSet((IOperationReprocessor)this);
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
                if (context.TryGetOperation() is null)
                    throw; // No Operation -> no retry
                if (!this.WillRetry(error, out var transiency))
                    throw; // The error can't be reprocessed -> no retry

                if (transiency is not Transiency.SuperTransient)
                    TryIndex++;
                context.ChangeOperation(null);
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
