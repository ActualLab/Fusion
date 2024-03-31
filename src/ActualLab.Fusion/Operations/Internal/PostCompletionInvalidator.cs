using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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
    public async Task OnCommand(ICompletion command, CommandContext context, CancellationToken cancellationToken)
    {
        var completedCommand = command.UntypedCommand;
        var mayRequireInvalidation =
            MayRequireInvalidation(completedCommand)
            && !Computed.IsInvalidating();
        if (!mayRequireInvalidation) {
            await context.InvokeRemainingHandlers(cancellationToken).ConfigureAwait(false);
            return;
        }

        var oldOperation = context.Items.Get<IOperation>();
        var operation = command.Operation;
        context.SetOperation(operation);
        var invalidateScope = Computed.Invalidate();
        try {
            await TryInvalidate(context, completedCommand, operation.Items, null, cancellationToken).ConfigureAwait(false);
        }
        finally {
            context.SetOperation(oldOperation);
            invalidateScope.Dispose();
        }
    }

    public virtual bool MayRequireInvalidation(ICommand command)
    {
        var finalHandler = CommandHandlerResolver.GetCommandHandlerChain(command).FinalHandler as IMethodCommandHandler;
        if (finalHandler == null || finalHandler.ParameterTypes.Length != 2)
            return false;

        var service = Services.GetService(finalHandler.GetHandlerServiceType());
        return service is IComputeService;
    }

    public virtual bool RequiresInvalidation(
        ICommand command,
        [MaybeNullWhen(false)] out IMethodCommandHandler finalHandler)
    {
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

    protected virtual async ValueTask TryInvalidate(
        CommandContext context,
        ICommand command,
        OptionSet operationItems,
        Activity? activity,
        CancellationToken cancellationToken)
    {
        var operation = context.Operation();
        var oldOperationItems = operation.Items;
        var oldActivity = activity;
        operation.Items = operationItems;
        try {
            if (RequiresInvalidation(command, out var finalHandler)) {
                activity ??= StartActivity(command);
                Log.IfEnabled(Settings.LogLevel)
                    ?.Log(Settings.LogLevel, "Invalidating: {Command}", command);
                try {
                    await finalHandler.Invoke(command, context, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception e) when (!e.IsCancellationOf(cancellationToken)) {
                    Log.LogError(e, "Failed to invalidate {Command}", command);
                }
            }

            var nestedCommands = operationItems.GetOrDefault(ImmutableList<NestedCommandEntry>.Empty);
            foreach (var (nestedCommand, nestedOperationItems) in nestedCommands)
                await TryInvalidate(context, nestedCommand, nestedOperationItems, activity, cancellationToken)
                    .ConfigureAwait(false);
        }
        finally {
            if (oldActivity != activity)
                activity?.Dispose();
            operation.Items = oldOperationItems;
        }
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
