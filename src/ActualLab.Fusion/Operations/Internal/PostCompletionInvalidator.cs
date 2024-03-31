using System.Diagnostics;
using ActualLab.Fusion.Client.Interception;
using ActualLab.Fusion.Interception;
using ActualLab.Interception;
using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Fusion.Operations.Internal;

#pragma warning disable IL2055, IL2072

public class PostCompletionInvalidator(
        PostCompletionInvalidator.Options settings,
        IServiceProvider services
        ) : ICommandHandler<ICompletion>
{
    public record Options
    {
        public LogLevel LogLevel { get; init; } = LogLevel.Debug;
    }

    private ActivitySource? _activitySource;
    private CommandHandlerResolver? _commandHandlerResolver;
    private ILogger? _log;

    protected IServiceProvider Services { get; } = services;
    protected Options Settings { get; } = settings;
    protected ActivitySource ActivitySource
        => _activitySource ??= GetType().GetActivitySource();
    protected CommandHandlerResolver CommandHandlerResolver
        => _commandHandlerResolver ??= Services.GetRequiredService<CommandHandlerResolver>();
    protected ILogger Log
        => _log ??= Services.LogFor(GetType());

    [CommandFilter(Priority = FusionOperationsCommandHandlerPriority.PostCompletionInvalidator)]
    public async Task OnCommand(ICompletion command, CommandContext context, CancellationToken cancellationToken)
    {
        var originalCommand = command.UntypedCommand;
        var requiresInvalidation =
            RequiresInvalidation(originalCommand)
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
            log?.Log(Settings.LogLevel,
                "Invalidating via original command handler for '{CommandType}'",
                originalCommand.GetType());
            await context.Commander.Call(originalCommand, cancellationToken).ConfigureAwait(false);

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

    public virtual bool RequiresInvalidation(ICommand command)
    {
        var finalHandler = CommandHandlerResolver.GetCommandHandlerChain(command).FinalHandler;
        if (finalHandler is not IMethodCommandHandler methodCommandHandler)
            return false;

        var methodParameters = methodCommandHandler.Parameters;
        if (methodParameters.Length != 2)
            return false;

        var service = Services.GetService(finalHandler.GetHandlerServiceType());
        if (service is not IComputeService)
            return false;

        var interceptor = (service as IProxy)?.Interceptor;
        if (interceptor is ComputeServiceInterceptor)
            return true; // Pure compute service

        if (interceptor is not RpcHybridInterceptor hybridInterceptor)
            return false;
        if (hybridInterceptor.ClientInterceptor is not ClientComputeServiceInterceptor clientComputeServiceInterceptor)
            return false;

        var clientInterceptor = clientComputeServiceInterceptor.ClientInterceptor;
        if (clientInterceptor.GetMethodDef(methodCommandHandler.Method, service.GetType()) is not RpcMethodDef rpcMethodDef)
            return false;

        var arguments = (ArgumentList)ArgumentList.Types[2]
            .MakeGenericType(methodParameters[0].ParameterType, methodParameters[1].ParameterType)
            .CreateInstance(command, default(CancellationToken));
        var rpcPeer = hybridInterceptor.CallRouter.Invoke(rpcMethodDef, arguments);
        return rpcPeer == null;
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
            if (RequiresInvalidation(command)) {
                operation.Items = items;
                await context.Commander.Call(command, cancellationToken).ConfigureAwait(false);
            }
            var subcommands = items.GetOrDefault(ImmutableList<NestedCommandEntry>.Empty);
            if (!subcommands.IsEmpty)
                await InvokeNestedCommands(context, operation, subcommands, cancellationToken).ConfigureAwait(false);
        }
    }
}
