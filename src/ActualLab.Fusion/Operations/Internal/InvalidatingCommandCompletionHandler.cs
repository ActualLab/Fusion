using System.Diagnostics;
using ActualLab.CommandR.Diagnostics;
using ActualLab.CommandR.Operations;
using ActualLab.Fusion.Diagnostics;
using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Fusion.Operations.Internal;

/// <summary>
/// A command handler that replays commands during <see cref="ICompletion"/> processing
/// inside an <see cref="Invalidation"/> scope to trigger dependency invalidation.
/// </summary>
public class InvalidatingCommandCompletionHandler(
    InvalidatingCommandCompletionHandler.Options settings,
    IServiceProvider services
    ) : ICommandHandler<ICompletion>
{
    /// <summary>
    /// Configuration options for <see cref="InvalidatingCommandCompletionHandler"/>.
    /// </summary>
    public record Options
    {
        public LogLevel LogLevel { get; init; } = LogLevel.Debug;
        public bool CaptureCommandPayload { get; init; }
    }

    private static readonly ConcurrentDictionary<Type, bool> ReplayDisqualifiedCommandTypes = new();

    protected IServiceProvider Services { get; } = services;
    protected Options Settings { get; } = settings;
    protected CommandHandlerResolver CommandHandlerResolver
        => field ??= Services.GetRequiredService<CommandHandlerResolver>();
    protected RpcHub RpcHub => field ??= Services.GetRequiredService<RpcHub>();
    protected ILogger Log => field ??= Services.LogFor(GetType());

    [CommandFilter(Priority = FusionOperationsCommandHandlerPriority.InvalidatingCommandCompletionHandler)]
    public async Task OnCommand(ICompletion completion, CommandContext context, CancellationToken cancellationToken)
    {
        var operation = completion.Operation;
        var command = operation.Command;
        if (Invalidation.IsActive || !IsRequired(command, out _, out _)) {
            // The handler is unused for the current completion
            await context.InvokeRemainingHandlers(cancellationToken).ConfigureAwait(false);
            return;
        }

        var commandType = command.GetType().GetName();
        Log.IfEnabled(Settings.LogLevel)
            ?.Log(Settings.LogLevel, "Invalidating: {CommandType}", commandType);

        // "Finally" block disposes everything here
        var durationHistogram = FusionInstruments.InvalidationPassDuration.IfEnabled();
        var commandCountHistogram = FusionInstruments.InvalidationPassCommandCount.IfEnabled();
        var startedAt = durationHistogram is not null ? CpuTimestamp.Now : default;
        var activity = StartActivity(command);
        var passState = activity is not null || durationHistogram is not null || commandCountHistogram is not null
            ? new InvalidationPassState(activity)
            : null;
        var operationItems = operation.Items;
        var oldOperation = context.TryGetOperation();
        context.ChangeOperation(operation);
        var invalidateScope = Invalidation.Begin(new InvalidationSource($"{commandType}'s invalidation pass"));
        var outcome = "success";
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
                index = await TryInvalidate(
                        context, operation, nestedCommand, nestedOperationItems.ToMutable(), index, passState)
                    .ConfigureAwait(false);
            }
            await TryInvalidate(context, operation, command, operationItems, index, passState).ConfigureAwait(false);
        }
        catch (Exception e) {
            outcome = "error";
            activity?.Finalize(e, cancellationToken);
            throw;
        }
        finally {
            invalidateScope.Dispose();
            context.ChangeOperation(oldOperation);
            if (passState is { HasError: true })
                outcome = "error";
            if (durationHistogram is not null || commandCountHistogram is not null) {
                var tags = new TagList {
                    { "command.name", commandType },
                    { "outcome", outcome },
                };
                durationHistogram?.Record(startedAt.Elapsed.TotalMilliseconds, tags);
                commandCountHistogram?.Record(passState?.CommandCount ?? 0, tags);
            }
            activity?.Dispose();
        }
    }

    public virtual bool IsRequired(ICommand? command,
        [MaybeNullWhen(false)] out IMethodCommandHandler finalHandler,
        out RpcServiceDef? rpcServiceDef)
    {
        rpcServiceDef = null;
        if (command is null or IDelegatingCommand) {
            finalHandler = null;
            return false;
        }

        var handler = CommandHandlerResolver.GetCommandHandlerChain(command).FinalHandler;
        finalHandler = handler as IMethodCommandHandler;
        if (finalHandler is null || finalHandler.ParameterTypes.Length != 2) {
            LogIfReplayDisqualified(command, handler);
            return false;
        }

        rpcServiceDef = RpcHub.ServiceRegistry.Get(finalHandler.ServiceType);
        if (rpcServiceDef is { Mode: RpcServiceMode.Client }) {
            // The command is handled by a pure RPC client. It means that:
            // - Another host will process it, and thus it is responsible for adding it to the operation log, etc.
            // - This host cannot process its invalidation anyway, since Invalidation.Begin() block
            //   enforces local routing for any command method call (see RpcOutboundCallOptionsExt.RouterFactory)
            //   and this host doesn't have a service (server) to handle such a call.
            return false;
        }

        return TryGetService(finalHandler.GetHandlerServiceType()) is IComputeService;
    }

    protected virtual async ValueTask<int> TryInvalidate(
        CommandContext context,
        Operation operation,
        ICommand command,
        MutablePropertyBag operationItems,
        int index,
        InvalidationPassState? passState)
    {
        if (!IsRequired(command, out var finalHandler, out var rpcServiceDef))
            return index;

        if (passState is not null)
            passState.CommandCount++;
        operation.Items = operationItems;
        Log.IfEnabled(Settings.LogLevel)?.Log(Settings.LogLevel,
            "- Invalidation #{Index}: {Service}.{Method} <- {Command}",
            index, finalHandler.ServiceType.GetName(), finalHandler.Method.Name, command);
        try {
            Task task;
            if (rpcServiceDef is { Mode: RpcServiceMode.Distributed }
                && rpcServiceDef.GetMethod(finalHandler.Method) is not null) {
                // We're going to execute the distributed service method directly here,
                // so make RpcInterceptor to execute it locally, without any kind of rerouting / reprocessing.
                using (new RpcOutboundCallSetup(RpcHub.LocalPeer).Activate())
                    task = finalHandler.Invoke(command, context, default);
            }
            else
                task = finalHandler.Invoke(command, context, default);
            await task.ConfigureAwait(false);
        }
        catch (Exception e) {
            passState?.RegisterFailure(e);
            Log.LogError(
                "Invalidation #{Index} failed: {Service}.{Method} <- {Command}",
                index, finalHandler.ServiceType.GetName(), finalHandler.Method.Name, command);
        }
        return index + 1;
    }

    protected virtual Activity? StartActivity(ICommand command)
    {
        var commandName = command.GetType().GetName();
        var operationName = $"-inv.{DiagnosticsExt.FixName(commandName)}";
        var activity = FusionInstruments.ActivitySource.StartActivity(operationName);
        activity?.AddCommandTags(command, Settings.CaptureCommandPayload);
        return activity;
    }

    // Private methods

    private void LogIfReplayDisqualified(ICommand command, CommandHandler? handler)
    {
        if (handler is null)
            return;

        var commandType = command.GetType();
        if (ReplayDisqualifiedCommandTypes.ContainsKey(commandType))
            return;
        if (TryGetService(handler.GetHandlerServiceType()) is not IComputeService)
            return;

        // TryAdd must run only when the log level is enabled - otherwise the one-shot is
        // consumed while logging is off and the message never appears once it's re-enabled.
        var log = Log.IfEnabled(LogLevel.Information);
        if (log is null)
            return;
        if (!ReplayDisqualifiedCommandTypes.TryAdd(commandType, true))
            return;

        log.LogInformation(
            "Invalidation replay is unsupported for {CommandType}: its final handler on {ServiceType} " +
            "isn't a 2-parameter method handler",
            commandType.GetName(), handler.GetHandlerServiceType().GetName());
    }

    private object? TryGetService(Type serviceType)
    {
        // The compute-service check resolves the service from the root provider, which throws
        // for scoped services under ValidateScopes. Registration-time checks make a scoped
        // compute service with command handlers unregistrable, so a failing resolution here
        // can only be a legal scoped non-compute service - i.e. no replay is needed for it.
        try {
            return Services.GetService(serviceType);
        }
        catch (Exception e) {
            Log.IfEnabled(LogLevel.Debug)?.LogDebug(e,
                "Cannot resolve '{ServiceType}' from the root service provider", serviceType.GetName());
            return null;
        }
    }

    // Nested types

    protected sealed class InvalidationPassState(Activity? activity)
    {
        private Activity? Activity { get; } = activity;

        public int CommandCount { get; set; }
        public bool HasError { get; private set; }
        public int FailureCount { get; private set; }

        public void RegisterFailure(Exception error)
        {
            HasError = true;
            FailureCount++;
            if (Activity is not { } activity)
                return;

            activity.SetTag("invalidation.partial_failure", true);
            activity.SetTag("invalidation.failure.count", FailureCount);
            activity.SetStatus(ActivityStatusCode.Error, "One or more invalidation commands failed.");
            if (!activity.IsAllDataRequested)
                return;

            var tags = new ActivityTagsCollection {
                { "exception.type", error.GetType().FullName ?? error.GetType().Name },
            };
            try {
                tags.Add("exception.message", error.Message);
                tags.Add("exception.stacktrace", error.ToString());
            }
            catch {
                // The failure is already visible via its type, status, and partial-failure tags.
            }
            activity.AddEvent(new ActivityEvent("exception", tags: tags));
        }
    }
}
