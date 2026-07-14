using ActualLab.CommandR.Operations;
using ActualLab.Fusion.Operations.Internal;
using ActualLab.Reflection;
using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Fusion.Tests.OperationEvents;

// Reusable "must not throw" harness for the no-fail contract of operation-completion listeners
// and the invalidation replay pass (docs/tasks/invalidation-audit.md, items 1 and 2).
public static class OperationCompletionNoThrowTester
{
    public static async Task AssertCompletionListenersDoNotThrow(
        IServiceProvider services, Operation operation, CommandContext? commandContext = null)
    {
        var listeners = services.GetServices<IOperationCompletionListener>().ToList();
        listeners.Should().NotBeEmpty("the harness is pointless without at least one registered listener");
        foreach (var listener in listeners) {
            Func<Task> act = () => listener.OnOperationCompleted(operation, commandContext);
            await act.Should().NotThrowAsync(
                $"'{listener.GetType().GetName()}' must never throw on operation completion");
        }
    }

    public static async Task AssertInvalidationPassDoesNotThrow(ICommander commander, Operation operation)
    {
        Func<Task> act = () => ReplayInvalidation(commander, operation);
        await act.Should().NotThrowAsync("the invalidation replay pass must never throw");
    }

    // Private methods

    // Mirrors InvalidatingCommandCompletionHandler's replay (OnCommand + TryInvalidate) minus its
    // swallow-and-log catch, so a throwing Invalidation.IsActive branch faults the returned task.
    private static async Task ReplayInvalidation(ICommander commander, Operation operation)
    {
        var services = commander.Services;
        var handler = services.GetRequiredService<InvalidatingCommandCompletionHandler>();
        var rpcHub = services.RpcHub();
        var command = operation.Command;
        var commandType = command.GetType().GetName();

        var completion = Completion.New(operation);
        var context = CommandContext.New(commander, completion, isOutermost: true);
        var operationItems = operation.Items;
        var oldOperation = context.TryGetOperation();
        context.ChangeOperation(operation);
        // The completion is an ISystemCommand, so activating its context lets the command-service
        // interceptor allow the direct finalHandler.Invoke calls below (as it does in production).
        using var _ = context.Activate();
        var invalidateScope = Invalidation.Begin(new InvalidationSource($"{commandType}'s invalidation pass"));
        try {
            foreach (var (nestedCommand, nestedItems) in operation.NestedOperations)
                await Invoke(nestedCommand, nestedItems.ToMutable()).ConfigureAwait(false);
            await Invoke(command, operationItems).ConfigureAwait(false);
        }
        finally {
            invalidateScope.Dispose();
            context.ChangeOperation(oldOperation);
        }
        return;

        async Task Invoke(ICommand invalidatedCommand, MutablePropertyBag items) {
            if (!handler.IsRequired(invalidatedCommand, out var finalHandler, out var rpcServiceDef))
                return;

            operation.Items = items;
            Task task;
            if (rpcServiceDef is { Mode: RpcServiceMode.Distributed }
                && rpcServiceDef.GetMethod(finalHandler.Method) is not null) {
                using (new RpcOutboundCallSetup(rpcHub.LocalPeer).Activate())
                    task = finalHandler.Invoke(invalidatedCommand, context, default);
            }
            else
                task = finalHandler.Invoke(invalidatedCommand, context, default);
            await task.ConfigureAwait(false);
        }
    }
}
