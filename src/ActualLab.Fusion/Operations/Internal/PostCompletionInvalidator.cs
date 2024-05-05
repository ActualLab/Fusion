using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using ActualLab.CommandR.Operations;
using ActualLab.Fusion.Client.Interception;
using ActualLab.Fusion.Interception;
using ActualLab.Interception;
using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Fusion.Operations.Internal;

public class PostCompletionInvalidator(
        PostCompletionInvalidator.Options settings,
        IServiceProvider services
        ) : ICommandHandler<ICompletion>
{
    public record Options
    {
        public LogLevel LogLevel { get; init; } = LogLevel.Debug;
    }

    private static readonly MethodInfo ArgumentListNewMethod = typeof(ArgumentList)
        .GetMethods(BindingFlags.Static | BindingFlags.Public)
        .SingleOrDefault(m => Equals(m.Name, nameof(ArgumentList.New)) && m.GetGenericArguments().Length == 2)!;

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
    public async Task OnCommand(ICompletion completion, CommandContext context, CancellationToken cancellationToken)
    {
        var operation = completion.Operation;
        var command = operation.Command;
        var mayRequireInvalidation =
            MayRequireInvalidation(command)
            && !Invalidation.IsActive;
        if (!mayRequireInvalidation) {
            await context.InvokeRemainingHandlers(cancellationToken).ConfigureAwait(false);
            return;
        }

        Log.IfEnabled(Settings.LogLevel)
            ?.Log(Settings.LogLevel, "Invalidating: {CommandType}", command.GetType());
        using var activity = StartActivity(command);

        var operationItems = operation.Items;
        var oldOperation = context.TryGetOperation();
        context.ChangeOperation(operation);
        var invalidateScope = Invalidation.Begin();
        try {
            // If we care only about the eventual consistency, the invalidation order
            // doesn't matter:
            // - Any node N gets invalidated when either it or any of its
            //   dependencies D[i] is invalidated.
            // - If you invalidate a subset of nodes in { N, D... } set in
            //   any order (and with any delays between the invalidations),
            //   the last invalidated dependency causes N to invalidate no matter what -
            //   assuming the current version of N still depends it.
            var index = 1;
            foreach (var (nestedCommand, nestedOperationItems) in operation.NestedOperations) {
                index = await TryInvalidate(context, operation, nestedCommand, nestedOperationItems.ToMutable(), index)
                    .ConfigureAwait(false);
            }
            await TryInvalidate(context, operation, command, operationItems, index).ConfigureAwait(false);
        }
        finally {
            invalidateScope.Dispose();
            context.ChangeOperation(oldOperation);
        }
    }

    public virtual bool MayRequireInvalidation(ICommand? command)
    {
        if (command is null or IApiCommand)
            return false;

        var finalHandler = CommandHandlerResolver.GetCommandHandlerChain(command).FinalHandler as IMethodCommandHandler;
        if (finalHandler == null || finalHandler.ParameterTypes.Length != 2)
            return false;

        var service = Services.GetService(finalHandler.GetHandlerServiceType());
        return service is IComputeService;
    }

    public virtual bool RequiresInvalidation(
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
        if (clientInterceptor.GetMethodDef(finalHandler.Method, service.GetType()) is not RpcMethodDef rpcMethodDef)
            return false;

        var arguments = (ArgumentList)ArgumentListNewMethod
            .MakeGenericMethod(finalHandler.ParameterTypes)
            .Invoke(null, [command, default(CancellationToken)])!;
        var rpcPeer = hybridInterceptor.CallRouter.Invoke(rpcMethodDef, arguments);
        return rpcPeer == null;
    }

    protected virtual async ValueTask<int> TryInvalidate(
        CommandContext context,
        Operation operation,
        ICommand command,
        MutablePropertyBag operationItems,
        int index)
    {
        if (!RequiresInvalidation(command, out var handler))
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
        var operationName = command.GetType().GetOperationName("Invalidate");
        var activity = ActivitySource.StartActivity(operationName);
        if (activity != null) {
            var tags = new ActivityTagsCollection { { "command", command.ToString() } };
            var activityEvent = new ActivityEvent(operationName, tags: tags);
            activity.AddEvent(activityEvent);
        }
        return activity;
    }
}
