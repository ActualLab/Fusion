using System.Diagnostics;

namespace Stl.Fusion.Operations.Internal;

public class PostCompletionInvalidator(
        PostCompletionInvalidator.Options settings,
        IServiceProvider services
        ) : ICommandHandler<ICompletion>
{
    public record Options
    {
        public LogLevel LogLevel { get; init; } = LogLevel.None;
    }

    private ActivitySource? _activitySource;
    private InvalidationInfoProvider? _invalidationInfoProvider;
    private ILogger? _log;

    protected IServiceProvider Services { get; } = services;
    protected Options Settings { get; } = settings;
    protected ActivitySource ActivitySource
        => _activitySource ??= GetType().GetActivitySource();
    protected InvalidationInfoProvider InvalidationInfoProvider
        => _invalidationInfoProvider ??= Services.GetRequiredService<InvalidationInfoProvider>();
    protected ILogger Log
        => _log ??= Services.LogFor(GetType());

    [CommandFilter(Priority = FusionOperationsCommandHandlerPriority.PostCompletionInvalidator)]
    public async Task OnCommand(ICompletion command, CommandContext context, CancellationToken cancellationToken)
    {
        var originalCommand = command.UntypedCommand;
        var requiresInvalidation =
            InvalidationInfoProvider.RequiresInvalidation(originalCommand)
            && !Computed.IsInvalidating();
        if (!requiresInvalidation) {
            await context.InvokeRemainingHandlers(cancellationToken).ConfigureAwait(false);
            return;
        }

        var oldOperation = context.Items.Get<IOperation>();
        var operation = command.Operation;
        context.SetOperation(operation);
        var invalidateScope = Computed.Invalidate();
        try {
            var log = Log.IfEnabled(Settings.LogLevel);
            using var activity = StartActivity(originalCommand);
            var finalHandler = context.ExecutionState.FindFinalHandler();
            var useOriginalCommandHandler = finalHandler == null
                || finalHandler.GetHandlerService(command, context) is CompletionTerminator;
            if (useOriginalCommandHandler) {
                if (InvalidationInfoProvider.IsClientComputeServiceCommand(originalCommand)) {
                    log?.Log(Settings.LogLevel,
                        "No invalidation for client compute service command '{CommandType}'",
                        originalCommand.GetType());
                    return;
                }
                log?.Log(Settings.LogLevel,
                    "Invalidating via original command handler for '{CommandType}'",
                    originalCommand.GetType());
                await context.Commander.Call(originalCommand, cancellationToken).ConfigureAwait(false);
            }
            else {
                log?.Log(Settings.LogLevel,
                    "Invalidating via dedicated command handler for '{CommandType}'",
                    command.GetType());
                await context.InvokeRemainingHandlers(cancellationToken).ConfigureAwait(false);
            }

            var operationItems = operation.Items;
            try {
                var nestedCommands = operationItems.GetOrDefault(ImmutableList<NestedCommandEntry>.Empty);
                if (!nestedCommands.IsEmpty)
                    await InvokeNestedCommands(context, operation, nestedCommands, cancellationToken).ConfigureAwait(false);
            }
            finally {
                operation.Items = operationItems;
            }
        }
        finally {
            context.SetOperation(oldOperation);
            invalidateScope.Dispose();
        }
    }

    protected virtual Activity? StartActivity(ICommand originalCommand)
    {
        var operationName = originalCommand.GetType().GetOperationName("Invalidate");
        var activity = ActivitySource.StartActivity(operationName);
        if (activity != null) {
            var tags = new ActivityTagsCollection { { "originalCommand", originalCommand.ToString() } };
            var activityEvent = new ActivityEvent(operationName, tags: tags);
            activity.AddEvent(activityEvent);
        }
        return activity;
    }

    protected virtual async ValueTask InvokeNestedCommands(
        CommandContext context,
        IOperation operation,
        ImmutableList<NestedCommandEntry> nestedCommands,
        CancellationToken cancellationToken)
    {
        foreach (var commandEntry in nestedCommands) {
            var (command, items) = commandEntry;
            // if (command is IBackendCommand backendCommand)
            //     backendCommand.MarkValid();
            if (InvalidationInfoProvider.RequiresInvalidation(command)) {
                operation.Items = items;
                await context.Commander.Call(command, cancellationToken).ConfigureAwait(false);
            }
            var subcommands = items.GetOrDefault(ImmutableList<NestedCommandEntry>.Empty);
            if (!subcommands.IsEmpty)
                await InvokeNestedCommands(context, operation, subcommands, cancellationToken).ConfigureAwait(false);
        }
    }
}
