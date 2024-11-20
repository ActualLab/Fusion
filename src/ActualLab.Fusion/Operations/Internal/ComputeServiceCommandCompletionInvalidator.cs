using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using ActualLab.CommandR.Operations;
using ActualLab.Fusion.Diagnostics;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Fusion.Operations.Internal;

public class ComputeServiceCommandCompletionInvalidator(
    ComputeServiceCommandCompletionInvalidator.Options settings,
    IServiceProvider services
    ) : ICommandHandler<ICompletion>
{
    public record Options
    {
        public LogLevel LogLevel { get; init; } = LogLevel.Debug;
    }

    private CommandHandlerResolver? _commandHandlerResolver;
    private ILogger? _log;

    protected IServiceProvider Services { get; } = services;
    protected Options Settings { get; } = settings;
    protected CommandHandlerResolver CommandHandlerResolver
        => _commandHandlerResolver ??= Services.GetRequiredService<CommandHandlerResolver>();
    protected ILogger Log => _log ??= Services.LogFor(GetType());

    [CommandFilter(Priority = FusionOperationsCommandHandlerPriority.ComputeServiceCommandCompletionInvalidator)]
    public async Task OnCommand(ICompletion completion, CommandContext context, CancellationToken cancellationToken)
    {
        var operation = completion.Operation;
        var command = operation.Command;
        if (Invalidation.IsActive || !IsRequired(command, out _)) {
            // The handler is unused for the current completion
            await context.InvokeRemainingHandlers(cancellationToken).ConfigureAwait(false);
            return;
        }

        Log.IfEnabled(Settings.LogLevel)
            ?.Log(Settings.LogLevel, "Invalidating: {CommandType}", command.GetType());

        // "Finally" block disposes everything here
        var activity = StartActivity(command);
        var operationItems = operation.Items;
        var oldOperation = context.TryGetOperation();
        context.ChangeOperation(operation);
        var invalidateScope = Invalidation.Begin();
        var suppressRpcScope = SuppressRpc();
        try {
            // If we care only about the eventual consistency, the invalidation order
            // doesn't matter:
            // - Any node N gets invalidated when either it or any of its
            //   dependencies D[i] is invalidated.
            // - If you invalidate a subset of nodes in { N, D... } set in
            //   any order (and with any delays between the invalidations),
            //   the last invalidated dependency causes N to invalidate no matter what -
            //   assuming the current version of N still depends on it.
            var index = 1;
            foreach (var (nestedCommand, nestedOperationItems) in operation.NestedOperations) {
                index = await TryInvalidate(context, operation, nestedCommand, nestedOperationItems.ToMutable(), index)
                    .ConfigureAwait(false);
            }
            await TryInvalidate(context, operation, command, operationItems, index).ConfigureAwait(false);
        }
        catch (Exception e) {
            activity?.Finalize(e, cancellationToken);
            throw;
        }
        finally {
            suppressRpcScope.Dispose();
            invalidateScope.Dispose();
            context.ChangeOperation(oldOperation);
            activity?.Dispose();
        }
    }

    public virtual bool IsRequired(
        ICommand? command,
        [MaybeNullWhen(false)] out IMethodCommandHandler finalHandler)
    {
        if (command is null or IApiCommand) {
            finalHandler = null;
            return false;
        }

        finalHandler = CommandHandlerResolver.GetCommandHandlerChain(command).FinalHandler as IMethodCommandHandler;
        if (finalHandler == null || finalHandler.ParameterTypes.Length != 2)
            return false;

        var service = Services.GetService(finalHandler.GetHandlerServiceType());
        return service is IComputeService;
    }

    protected virtual async ValueTask<int> TryInvalidate(
        CommandContext context,
        Operation operation,
        ICommand command,
        MutablePropertyBag operationItems,
        int index)
    {
        if (!IsRequired(command, out var handler))
            return index;

        operation.Items = operationItems;
        Log.IfEnabled(Settings.LogLevel)?.Log(Settings.LogLevel,
            "- Invalidation #{Index}: {Service}.{Method} <- {Command}",
            index, handler.ServiceType.GetName(), handler.Method.Name, command);
        try {
            await handler.Invoke(command, context, default).ConfigureAwait(false);
        }
        catch (Exception) {
            Log.LogError(
                "Invalidation #{Index} failed: {Service}.{Method} <- {Command}",
                index, handler.ServiceType.GetName(), handler.Method.Name, command);
        }
        return index + 1;
    }

    protected virtual Activity? StartActivity(ICommand command)
    {
        var operationName = command.GetOperationName("", "-inv");
        var activity = FusionInstruments.ActivitySource.StartActivity(operationName);
        if (activity != null) {
            var tags = new ActivityTagsCollection { { "command", command.ToString() } };
            var activityEvent = new ActivityEvent(operationName, tags: tags);
            activity.AddEvent(activityEvent);
        }
        return activity;
    }

    protected static RpcOutboundContext.Scope SuppressRpc()
    {
        var context = new RpcOutboundContext() {
            Suppressor = static (method, _) => TaskExt.FromDefaultResult(method.UnwrappedReturnType),
        };
        return context.Activate();
    }
}
